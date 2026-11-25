using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System.Globalization;
using System.Text;

namespace DeskVault.UI.Rendering.HtmlDocumentRendering;

/// <summary>
/// Validates untrusted HTML markup for compliance with the restricted
/// embedded-resource policy used by the in-app document preview.
/// </summary>
/// <remarks>
/// The policy permits inline styling and explicitly approved embedded
/// PNG/JPEG images. It rejects external resource references, CSS URL
/// functions, imported stylesheets and unsupported active document elements.
///
/// This class validates rather than rewrites the source markup. A document
/// containing a prohibited construct must be rejected by its caller.
/// </remarks>
internal static class HtmlDocumentEmbeddedResourcePolicy
{
    private static readonly HashSet<string> ForbiddenElementNames =
    [
        "script",
        "iframe",
        "frame",
        "frameset",
        "object",
        "embed",
        "applet",
        "svg",
        "math",
        "video",
        "audio",
        "source",
        "track",
        "picture",
        "portal",
        "form",
        "input",
        "button",
        "select",
        "textarea",
        "option",
        "fieldset",
        "output",
        "base",
        "canvas"
    ];

    private static readonly HashSet<string> ResourceBearingAttributeNames =
    [
        "src",
        "srcset",
        "href",
        "poster",
        "data",
        "action",
        "formaction",
        "background",
        "cite",
        "longdesc",
        "manifest",
        "ping",
        "codebase",
        "archive",
        "classid",
        "usemap",
        "xlink:href",
        "xml:base",
        "srcdoc"
    ];

    /// <summary>
    /// Determines whether the supplied HTML markup complies with the
    /// restricted embedded-resource and inline-CSS policy.
    /// </summary>
    /// <param name="markup">
    /// The untrusted HTML document to validate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the markup contains no prohibited
    /// resource references or unsupported active document elements;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// HTML parsing is performed without enabling scripting or configuring
    /// a resource loader. CSS is parsed synchronously so validation does not
    /// initiate network requests.
    /// </remarks>
    internal static bool IsMarkupAllowed(
        string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);

        var htmlParser = new HtmlParser(
            new HtmlParserOptions
            {
                IsScripting = false
            });

        IHtmlDocument document =
            htmlParser.ParseDocument(markup);

        if (!AreElementsAllowed(document))
        {
            return false;
        }

        if (!AreInlineStylesAllowed(document))
        {
            return false;
        }

        return AreStyleElementsAllowed(document);
    }

    /// <summary>
    /// Verifies that document elements do not introduce prohibited
    /// active capabilities or external resource references.
    /// </summary>
    private static bool AreElementsAllowed(
        IHtmlDocument document)
    {
        foreach (IElement element in document.QuerySelectorAll("*"))
        {
            string elementName =
                element.LocalName;

            if (ForbiddenElementNames.Contains(elementName))
            {
                return false;
            }

            if (elementName.Equals(
                    "meta",
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    element.GetAttribute("http-equiv"),
                    "refresh",
                    StringComparison.OrdinalIgnoreCase))
            {
                // Meta refresh can initiate document navigation independently
                // of the user's interaction with the rendered document.
                return false;
            }

            foreach (IAttr attribute in element.Attributes)
            {
                string attributeName =
                    attribute.Name;

                if (attributeName.StartsWith(
                        "on",
                        StringComparison.OrdinalIgnoreCase))
                {
                    // Event-handler attributes are rejected rather than
                    // relying exclusively on JavaScript being disabled.
                    return false;
                }

                if (attributeName.Equals(
                        "style",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!ResourceBearingAttributeNames.Contains(attributeName))
                {
                    continue;
                }

                if (attributeName.Equals(
                        "src",
                        StringComparison.OrdinalIgnoreCase)
                    && elementName.Equals(
                        "img",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (!HtmlDocumentRenderingSecurityPolicy
                            .IsApprovedEmbeddedImageDataUri(
                                attribute.Value))
                    {
                        return false;
                    }

                    continue;
                }

                if (attributeName.Equals(
                        "href",
                        StringComparison.OrdinalIgnoreCase)
                    && IsLocalFragmentNavigation(
                        elementName,
                        attribute.Value))
                {
                    continue;
                }

                // Every other resource-bearing attribute is prohibited.
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether an anchor reference is confined to the
    /// currently displayed document.
    /// </summary>
    private static bool IsLocalFragmentNavigation(
        string elementName,
        string? value)
    {
        bool supportedElement =
            elementName.Equals(
                "a",
                StringComparison.OrdinalIgnoreCase)
            || elementName.Equals(
                "area",
                StringComparison.OrdinalIgnoreCase);

        if (!supportedElement
            || string.IsNullOrEmpty(value)
            || value[0] != '#')
        {
            return false;
        }

        return !value.Any(char.IsControl)
               && !value.Contains('\\');
    }

    /// <summary>
    /// Validates CSS declarations supplied through HTML style attributes.
    /// </summary>
    private static bool AreInlineStylesAllowed(
        IHtmlDocument document)
    {
        foreach (IElement element in document.QuerySelectorAll("[style]"))
        {
            string declarationText =
                element.GetAttribute("style") ?? string.Empty;

            if (!IsStyleDeclarationAllowed(declarationText))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates the contents of every inline style element.
    /// </summary>
    private static bool AreStyleElementsAllowed(
        IHtmlDocument document)
    {
        foreach (IElement styleElement in document.QuerySelectorAll("style"))
        {
            if (!IsStyleSheetAllowed(styleElement.TextContent))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses and validates a CSS declaration block.
    /// </summary>
    private static bool IsStyleDeclarationAllowed(
        string declarationText)
    {
        var parser = CreateCssParser();

        bool parseErrorOccurred = false;

        parser.Error +=
            (_, _) => parseErrorOccurred = true;

        ICssStyleDeclaration? declaration =
            parser.ParseDeclaration(declarationText);

        if (parseErrorOccurred || declaration is null)
        {
            return false;
        }

        return AreDeclarationsAllowed(declaration);
    }

    /// <summary>
    /// Parses and validates a CSS stylesheet without loading imports
    /// or external resources.
    /// </summary>
    private static bool IsStyleSheetAllowed(
        string styleSheetText)
    {
        var parser = CreateCssParser();

        bool parseErrorOccurred = false;

        parser.Error +=
            (_, _) => parseErrorOccurred = true;

        ICssStyleSheet styleSheet =
            parser.ParseStyleSheet(styleSheetText);

        return !parseErrorOccurred
               && AreRulesAllowed(styleSheet.Rules);
    }

    /// <summary>
    /// Creates a CSS parser configured to retain unknown declarations
    /// and rules so that unsupported constructs cannot silently disappear
    /// during security validation.
    /// </summary>
    private static CssParser CreateCssParser()
    {
        return new CssParser(
            new CssParserOptions
            {
                IsIncludingUnknownDeclarations = true,
                IsIncludingUnknownRules = true
            });
    }

    /// <summary>
    /// Verifies that CSS declarations contain no URL function references.
    /// </summary>
    private static bool AreDeclarationsAllowed(
        ICssProperties declarations)
    {
        foreach (ICssProperty property in declarations)
        {
            if (ContainsCssUrlFunction(property.Value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates CSS rules recursively, including supported grouping rules
    /// and nested style rules.
    /// </summary>
    private static bool AreRulesAllowed(
        ICssRuleList rules)
    {
        foreach (ICssRule rule in rules)
        {
            // Inspect serialized rule text as well as parsed declarations.
            // This also covers resource references in rule preludes.
            if (ContainsCssUrlFunction(rule.CssText))
            {
                return false;
            }

            switch (rule.Type)
            {
                case CssRuleType.Style:
                    if (rule is not ICssStyleRule styleRule
                        || !AreDeclarationsAllowed(styleRule.Style)
                        || !AreRulesAllowed(styleRule.Rules))
                    {
                        return false;
                    }

                    break;

                case CssRuleType.Media:
                case CssRuleType.Supports:
                case CssRuleType.Container:
                case CssRuleType.Layer:
                case CssRuleType.Scope:
                case CssRuleType.StartingStyle:
                    if (rule is not ICssGroupingRule groupingRule
                        || !AreRulesAllowed(groupingRule.Rules))
                    {
                        return false;
                    }

                    break;

                case CssRuleType.Keyframes:
                    if (rule is not ICssKeyframesRule keyframesRule
                        || !AreRulesAllowed(keyframesRule.Rules))
                    {
                        return false;
                    }

                    break;

                case CssRuleType.Keyframe:
                    if (rule is not ICssKeyframeRule keyframeRule
                        || !AreDeclarationsAllowed(keyframeRule.Style))
                    {
                        return false;
                    }

                    break;

                case CssRuleType.Charset:
                case CssRuleType.Comment:
                    break;

                default:
                    // Import, font-face, namespace, unknown and other
                    // unsupported at-rules are rejected by default.
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Detects CSS url() function tokens in parser-normalized CSS text.
    /// Quoted strings and comments are skipped so ordinary text containing
    /// the characters "url(" is not treated as a resource reference.
    /// </summary>
    private static bool ContainsCssUrlFunction(
        string cssText)
    {
        for (int index = 0; index < cssText.Length;)
        {
            char current =
                cssText[index];

            if (current == '/'
                && index + 1 < cssText.Length
                && cssText[index + 1] == '*')
            {
                if (!SkipCssComment(cssText, ref index))
                {
                    // Unterminated comments are invalid input.
                    return true;
                }

                continue;
            }

            if (current is '"' or '\'')
            {
                SkipCssString(
                    cssText,
                    ref index,
                    current);

                continue;
            }

            if (IsCssIdentifierStart(current))
            {
                string identifier =
                    ReadCssIdentifier(
                        cssText,
                        ref index);

                if (identifier.Equals(
                        "url",
                        StringComparison.OrdinalIgnoreCase)
                    && index < cssText.Length
                    && cssText[index] == '(')
                {
                    return true;
                }

                continue;
            }

            index++;
        }

        return false;
    }

    /// <summary>
    /// Reads a CSS identifier and decodes CSS escapes before comparison.
    /// </summary>
    private static string ReadCssIdentifier(
        string cssText,
        ref int index)
    {
        var identifier = new StringBuilder();

        while (index < cssText.Length)
        {
            char current =
                cssText[index];

            if (IsCssNameCharacter(current))
            {
                identifier.Append(current);
                index++;
                continue;
            }

            if (current == '\\')
            {
                if (!AppendCssEscape(
                        cssText,
                        ref index,
                        identifier))
                {
                    break;
                }

                continue;
            }

            break;
        }

        return identifier.ToString();
    }

    /// <summary>
    /// Decodes a CSS escape within an identifier.
    /// </summary>
    private static bool AppendCssEscape(
        string cssText,
        ref int index,
        StringBuilder destination)
    {
        // The current character must be the escape introducer.
        index++;

        if (index >= cssText.Length)
        {
            return false;
        }

        char escapedCharacter =
            cssText[index];

        if (escapedCharacter is '\r' or '\n' or '\f')
        {
            return false;
        }

        if (!Uri.IsHexDigit(escapedCharacter))
        {
            destination.Append(escapedCharacter);
            index++;
            return true;
        }

        int escapeStart =
            index;

        int escapeLength = 0;

        while (index < cssText.Length
               && escapeLength < 6
               && Uri.IsHexDigit(cssText[index]))
        {
            index++;
            escapeLength++;
        }

        if (!int.TryParse(
                cssText.AsSpan(
                    escapeStart,
                    escapeLength),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out int codePoint))
        {
            return false;
        }

        // Consume the optional whitespace terminator following a hex escape.
        if (index < cssText.Length
            && char.IsWhiteSpace(cssText[index]))
        {
            if (cssText[index] == '\r'
                && index + 1 < cssText.Length
                && cssText[index + 1] == '\n')
            {
                index += 2;
            }
            else
            {
                index++;
            }
        }

        if (codePoint == 0
            || codePoint > 0x10FFFF
            || codePoint is >= 0xD800 and <= 0xDFFF)
        {
            destination.Append('\uFFFD');
            return true;
        }

        destination.Append(
            char.ConvertFromUtf32(codePoint));

        return true;
    }

    /// <summary>
    /// Skips a CSS comment and advances the cursor beyond its terminator.
    /// </summary>
    private static bool SkipCssComment(
        string cssText,
        ref int index)
    {
        int terminatorIndex =
            cssText.IndexOf(
                "*/",
                index + 2,
                StringComparison.Ordinal);

        if (terminatorIndex < 0)
        {
            index = cssText.Length;
            return false;
        }

        index = terminatorIndex + 2;

        return true;
    }

    /// <summary>
    /// Skips a quoted CSS string while respecting escaped characters.
    /// </summary>
    private static void SkipCssString(
        string cssText,
        ref int index,
        char quote)
    {
        index++;

        while (index < cssText.Length)
        {
            char current =
                cssText[index++];

            if (current == '\\')
            {
                // The escaped character is data, not a string terminator.
                if (index < cssText.Length)
                {
                    index++;
                }

                continue;
            }

            if (current == quote)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Determines whether a character can begin a CSS identifier.
    /// </summary>
    private static bool IsCssIdentifierStart(
        char value)
    {
        return char.IsAsciiLetter(value)
               || value == '_'
               || value == '-'
               || value == '\\'
               || value >= '\u0080';
    }

    /// <summary>
    /// Determines whether a character can continue a CSS identifier.
    /// </summary>
    private static bool IsCssNameCharacter(
        char value)
    {
        return char.IsAsciiLetterOrDigit(value)
               || value == '_'
               || value == '-'
               || value >= '\u0080';
    }
}
