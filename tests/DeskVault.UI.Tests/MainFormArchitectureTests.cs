using DeskVault.UI.Forms;
using DeskVault.UI.Presenters;
using DeskVault.UI.Views;
using System.Reflection;

namespace DeskVault.UI.Tests;

public sealed class MainFormArchitectureTests
{
    private static readonly Type[] ForbiddenPresentationTypes =
    [
        typeof(DocumentViewForm),
        typeof(WorkspaceViewForm),
        typeof(DocumentWorkspacePresenter)
    ];

    private const BindingFlags DeclaredMembers =
        BindingFlags.Public |
        BindingFlags.NonPublic |
        BindingFlags.Instance |
        BindingFlags.Static |
        BindingFlags.DeclaredOnly;

    [Fact]
    public void MainForm_ImplementsApplicationShellView_WithoutConcreteChildPresentationDependencies()
    {
        // Arrange
        Type mainFormType = typeof(MainForm);

        // Act
        Type[] declaredMemberTypes =
            [.. GetDeclaredMemberTypes(mainFormType).SelectMany(ExpandType)];

        // Assert
        Assert.Contains(
            typeof(IMainFormView),
            mainFormType.GetInterfaces());

        foreach (Type forbiddenType in ForbiddenPresentationTypes)
        {
            Assert.DoesNotContain(
                forbiddenType,
                declaredMemberTypes);
        }
    }

    [Fact]
    public void MainFormPresenter_UsesViewContract_WithoutConcreteChildPresentationDependencies()
    {
        // Arrange
        Type presenterType = typeof(MainFormPresenter);

        ConstructorInfo constructor =
            Assert.Single(
                presenterType.GetConstructors(
                    BindingFlags.Public |
                    BindingFlags.Instance));

        Type[] parameterTypes =
            [.. constructor.GetParameters().Select(parameter => parameter.ParameterType)];

        Type[] declaredMemberTypes =
            [.. GetDeclaredMemberTypes(presenterType).SelectMany(ExpandType)];

        // Assert
        Assert.NotEmpty(parameterTypes);

        Assert.Equal(
            typeof(IMainFormView),
            parameterTypes[0]);

        foreach (Type forbiddenType in ForbiddenPresentationTypes)
        {
            Assert.DoesNotContain(
                forbiddenType,
                declaredMemberTypes);
        }
    }

    [Fact]
    public void MainFormViewContract_DoesNotExposeWorkspaceNavigationOrDocumentPresentationOperations()
    {
        // Arrange
        HashSet<string> actualMemberNames =
            typeof(IMainFormView)
                .GetMembers()
                .Select(member => member.Name)
                .ToHashSet(StringComparer.Ordinal);

        string[] forbiddenMemberNames =
        [
            // Workspace navigation and active-document ownership.
            "ActiveDocumentId",
            "NavigationMode",
            "PresentedDocumentIds",
            "DocumentPresentations",
            "DocumentActivated",
            "NavigationModeChanged",
            "ActivateDocument",
            "ActivateWorkspace",
            "CloseWorkspaceRequested",
            "CloseWorkspace",

            // Document presentation and Form hosting.
            "ShowDocumentAsync",
            "ShowUnsupportedPreview",
            "ShowDocumentInformation",
            "ClearPresentation",
            "AddDocumentPresentation",
            "RemoveDocumentPresentation"
        ];

        // Assert
        foreach (string forbiddenMemberName in forbiddenMemberNames)
        {
            Assert.DoesNotContain(
                forbiddenMemberName,
                actualMemberNames);
        }
    }

    [Fact]
    public void MainFormViewContract_ExposesFileSelectionAsAnActionInsteadOfState()
    {
        // Arrange
        Type viewContractType =
            typeof(IMainFormView);

        PropertyInfo? selectedFilePath =
            viewContractType.GetProperty("SelectedFilePath");

        MethodInfo? selectFileToImport =
            viewContractType.GetMethod(
                nameof(IMainFormView.SelectFileToImport),
                Type.EmptyTypes);

        // Assert
        Assert.Null(selectedFilePath);

        Assert.NotNull(selectFileToImport);

        Assert.Equal(
            typeof(string),
            selectFileToImport.ReturnType);

        Assert.Contains(
            typeof(IMainFormView),
            typeof(MainForm).GetInterfaces());
    }

    private static IEnumerable<Type> GetDeclaredMemberTypes(
        Type declaringType)
    {
        return
            declaringType
                .GetFields(DeclaredMembers)
                .Select(field => field.FieldType)
                .Concat(
                    declaringType
                        .GetProperties(DeclaredMembers)
                        .Select(property => property.PropertyType))
                .Concat(
                    declaringType
                        .GetEvents(DeclaredMembers)
                        .Where(@event => @event.EventHandlerType is not null)
                        .Select(@event => @event.EventHandlerType!))
                .Concat(
                    declaringType
                        .GetConstructors(DeclaredMembers)
                        .SelectMany(
                            constructor =>
                                constructor.GetParameters()
                                    .Select(parameter => parameter.ParameterType)))
                .Concat(
                    declaringType
                        .GetMethods(DeclaredMembers)
                        .SelectMany(
                            method =>
                                new[] { method.ReturnType }
                                    .Concat(
                                        method.GetParameters()
                                            .Select(parameter => parameter.ParameterType))));
    }

    private static IEnumerable<Type> ExpandType(Type type)
    {
        if (type.HasElementType &&
            type.GetElementType() is Type elementType)
        {
            foreach (Type nestedType in ExpandType(elementType))
            {
                yield return nestedType;
            }
        }

        if (type.IsGenericType)
        {
            foreach (Type genericArgument in type.GetGenericArguments())
            {
                foreach (Type nestedType in ExpandType(genericArgument))
                {
                    yield return nestedType;
                }
            }
        }

        yield return type;
    }
}
