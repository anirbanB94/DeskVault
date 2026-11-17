using DeskVault.Domain.Workspaces;
using DeskVault.UI.Forms;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Views;
using System.Reflection;

namespace DeskVault.UI.Tests;

public sealed class WorkspacePresentationArchitectureTests
{
    private const BindingFlags PublicInstanceProperties =
        BindingFlags.Instance |
        BindingFlags.Public;

    private const BindingFlags DeclaredPublicInstanceProperties =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.DeclaredOnly;

    private static readonly string[] AuthoritativeWorkspacePropertyNames =
    [
        nameof(WorkspacePresentation.WorkspaceId),
        nameof(WorkspacePresentation.WorkspaceType),
        nameof(WorkspacePresentation.WorkspaceName),
        nameof(WorkspacePresentation.WorkspaceDescription),
        nameof(WorkspacePresentation.IsTemporary),
        nameof(WorkspacePresentation.WorkspaceDocumentIds),
        nameof(WorkspacePresentation.PresentedDocumentIds),
        nameof(WorkspacePresentation.DocumentPresentations),
        nameof(WorkspacePresentation.NavigationMode),
        nameof(WorkspacePresentation.ActiveDocumentId)
    ];

    private const BindingFlags DeclaredInstanceMembers =
        BindingFlags.Instance |
        BindingFlags.Static |
        BindingFlags.Public |
        BindingFlags.NonPublic |
        BindingFlags.DeclaredOnly;

    [Fact]
    public void WorkspacePresentation_OwnsWorkspaceIdentityAndType()
    {
        // Arrange
        Type presentationType =
            typeof(WorkspacePresentation);

        PropertyInfo workspaceId =
            GetPublicProperty(
                presentationType,
                nameof(WorkspacePresentation.WorkspaceId));

        PropertyInfo workspaceType =
            GetPublicProperty(
                presentationType,
                nameof(WorkspacePresentation.WorkspaceType));

        PropertyInfo workspaceName =
            GetPublicProperty(
                presentationType,
                nameof(WorkspacePresentation.WorkspaceName));

        PropertyInfo workspaceDescription =
            GetPublicProperty(
                presentationType,
                nameof(WorkspacePresentation.WorkspaceDescription));

        PropertyInfo isTemporary =
            GetPublicProperty(
                presentationType,
                nameof(WorkspacePresentation.IsTemporary));

        // Assert
        Assert.Equal(
            typeof(Guid),
            workspaceId.PropertyType);

        AssertReadOnly(workspaceId);

        Assert.Equal(
            typeof(WorkspaceType),
            workspaceType.PropertyType);

        AssertPrivateSetter(workspaceType);

        Assert.Equal(
            typeof(string),
            workspaceName.PropertyType);

        AssertPrivateSetter(workspaceName);

        Assert.Equal(
            typeof(string),
            workspaceDescription.PropertyType);

        AssertPrivateSetter(workspaceDescription);

        Assert.Equal(
            typeof(bool),
            isTemporary.PropertyType);

        AssertReadOnly(isTemporary);
    }

    [Fact]
    public void WorkspacePresentation_OwnsMembershipPresentationAndSelectionState()
    {
        // Arrange
        Type presentationType =
            typeof(WorkspacePresentation);

        PropertyInfo workspaceDocumentIds =
            GetPublicProperty(
                presentationType,
                nameof(WorkspacePresentation.WorkspaceDocumentIds));

        PropertyInfo presentedDocumentIds =
            GetPublicProperty(
                presentationType,
                nameof(WorkspacePresentation.PresentedDocumentIds));

        PropertyInfo documentPresentations =
            GetPublicProperty(
                presentationType,
                nameof(WorkspacePresentation.DocumentPresentations));

        PropertyInfo navigationMode =
            GetPublicProperty(
                presentationType,
                nameof(WorkspacePresentation.NavigationMode));

        PropertyInfo activeDocumentId =
            GetPublicProperty(
                presentationType,
                nameof(WorkspacePresentation.ActiveDocumentId));

        // Assert
        Assert.Equal(
            typeof(IReadOnlyCollection<Guid>),
            workspaceDocumentIds.PropertyType);

        AssertReadOnly(workspaceDocumentIds);

        Assert.Equal(
            typeof(IReadOnlyCollection<Guid>),
            presentedDocumentIds.PropertyType);

        AssertReadOnly(presentedDocumentIds);

        Assert.Equal(
            typeof(IReadOnlyCollection<WorkspaceDocumentPresentation>),
            documentPresentations.PropertyType);

        AssertReadOnly(documentPresentations);

        Assert.Equal(
            typeof(WorkspacePresentationNavigationMode),
            navigationMode.PropertyType);

        AssertPrivateSetter(navigationMode);

        Assert.Equal(
            typeof(Guid?),
            activeDocumentId.PropertyType);

        AssertPrivateSetter(activeDocumentId);
    }

    [Fact]
    public void WorkspacePresentationViewContract_DoesNotExposeAuthoritativeWorkspaceState()
    {
        // Arrange
        string[] actualPropertyNames =
            typeof(IWorkspacePresentationView)
                .GetProperties()
                .Select(property => property.Name)
                .ToArray();

        // Assert
        foreach (string propertyName in AuthoritativeWorkspacePropertyNames)
        {
            Assert.DoesNotContain(
                propertyName,
                actualPropertyNames);
        }
    }

    [Fact]
    public void WorkspaceViewForm_ImplementsPassiveViewContractWithoutExposingAuthoritativeWorkspaceState()
    {
        // Arrange
        Type workspaceViewFormType =
            typeof(WorkspaceViewForm);

        string[] actualPropertyNames =
            workspaceViewFormType
                .GetProperties(DeclaredPublicInstanceProperties)
                .Select(property => property.Name)
                .ToArray();

        // Assert
        Assert.Contains(
            typeof(IWorkspacePresentationView),
            workspaceViewFormType.GetInterfaces());

        foreach (string propertyName in AuthoritativeWorkspacePropertyNames)
        {
            Assert.DoesNotContain(
                propertyName,
                actualPropertyNames);
        }
    }

    [Fact]
    public void WorkspaceDocumentPresentation_ExposesSeparateDocumentAndHostingContracts()
    {
        // Arrange
        Type presentationType =
            typeof(WorkspaceDocumentPresentation);

        PropertyInfo documentView =
            GetPublicProperty(
                presentationType,
                nameof(WorkspaceDocumentPresentation.View));

        PropertyInfo hostedForm =
            GetPublicProperty(
                presentationType,
                nameof(WorkspaceDocumentPresentation.HostedForm));

        // Assert
        Assert.Equal(
            typeof(IDocumentView),
            documentView.PropertyType);

        AssertReadOnly(documentView);

        Assert.Equal(
            typeof(Form),
            hostedForm.PropertyType);

        AssertReadOnly(hostedForm);
    }

    [Fact]
    public void WorkspaceViewForm_MemberSignaturesDoNotExposeDocumentViewForm()
    {
        // Arrange
        Type workspaceViewFormType =
            typeof(WorkspaceViewForm);

        Type[] memberSignatureTypes =
        [
            .. workspaceViewFormType
                .GetFields(DeclaredInstanceMembers)
                .Select(field => field.FieldType),

            .. workspaceViewFormType
                .GetProperties(DeclaredInstanceMembers)
                .SelectMany(property =>
                    new[]
                    {
                        property.PropertyType
                    }.Concat(
                        property.GetIndexParameters()
                            .Select(parameter => parameter.ParameterType))),

            .. workspaceViewFormType
                .GetMethods(DeclaredInstanceMembers)
                .SelectMany(method =>
                    new[]
                    {
                        method.ReturnType
                    }.Concat(
                        method.GetParameters()
                            .Select(parameter => parameter.ParameterType))),

            .. workspaceViewFormType
                .GetConstructors(DeclaredInstanceMembers)
                .SelectMany(constructor =>
                    constructor.GetParameters()
                        .Select(parameter => parameter.ParameterType)),

            .. workspaceViewFormType
                .GetEvents(DeclaredInstanceMembers)
                .Where(@event => @event.EventHandlerType is not null)
                .Select(@event => @event.EventHandlerType!)
        ];

        // Assert
        Assert.DoesNotContain(
            memberSignatureTypes,
            ReferencesDocumentViewForm);
    }

    private static bool ReferencesDocumentViewForm(Type type)
    {
        if (type == typeof(DocumentViewForm))
        {
            return true;
        }

        if (type.HasElementType &&
            type.GetElementType() is Type elementType &&
            ReferencesDocumentViewForm(elementType))
        {
            return true;
        }

        return type.IsGenericType &&
            type.GetGenericArguments()
                .Any(ReferencesDocumentViewForm);
    }

    private static PropertyInfo GetPublicProperty(
        Type declaringType,
        string propertyName)
    {
        PropertyInfo? property =
            declaringType.GetProperty(
                propertyName,
                PublicInstanceProperties);

        Assert.NotNull(property);

        return property!;
    }

    private static void AssertReadOnly(
        PropertyInfo property)
    {
        Assert.NotNull(
            property.GetMethod);

        Assert.Null(
            property.GetSetMethod(nonPublic: true));
    }

    private static void AssertPrivateSetter(
        PropertyInfo property)
    {
        MethodInfo? setter =
            property.GetSetMethod(nonPublic: true);

        Assert.NotNull(setter);

        Assert.Null(
            property.GetSetMethod());

        Assert.True(
            setter!.IsPrivate);
    }
}
