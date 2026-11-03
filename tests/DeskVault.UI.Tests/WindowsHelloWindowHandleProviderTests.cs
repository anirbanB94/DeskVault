using System.Runtime.ExceptionServices;
using DeskVault.Infrastructure.Security.Authentication;
using DeskVault.UI.Hosting;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class WindowsHelloWindowHandleProviderTests
{
    [Fact]
    public void GetWindowHandle_WhenNoActiveFormExists_ReturnsZero()
    {
        // Arrange
        var accessor =
            new Mock<IActiveWindowsHelloFormAccessor>();

        accessor
            .Setup(x => x.GetActiveForm())
            .Returns((Form?)null);

        var provider =
            new WindowsHelloWindowHandleProvider(
                accessor.Object);

        // Act
        IntPtr result =
            provider.GetWindowHandle();

        // Assert
        Assert.Equal(
            IntPtr.Zero,
            result);

        accessor.Verify(
            x => x.GetActiveForm(),
            Times.Once);
    }

    [Fact]
    public void GetWindowHandle_WhenFormHasNoCreatedHandle_ReturnsZero()
    {
        RunOnStaThread(
            () =>
            {
                // Arrange
                using var form = new Form();

                Assert.False(form.IsHandleCreated);

                var accessor =
                    new Mock<IActiveWindowsHelloFormAccessor>();

                accessor
                    .Setup(x => x.GetActiveForm())
                    .Returns(form);

                var provider =
                    new WindowsHelloWindowHandleProvider(
                        accessor.Object);

                // Act
                IntPtr result =
                    provider.GetWindowHandle();

                // Assert
                Assert.Equal(
                    IntPtr.Zero,
                    result);

                Assert.False(form.IsHandleCreated);
            });
    }

    [Fact]
    public void GetWindowHandle_WhenFormIsDisposed_ReturnsZero()
    {
        RunOnStaThread(
            () =>
            {
                // Arrange
                var form = new Form();

                _ = form.Handle;

                form.Dispose();

                var accessor =
                    new Mock<IActiveWindowsHelloFormAccessor>();

                accessor
                    .Setup(x => x.GetActiveForm())
                    .Returns(form);

                var provider =
                    new WindowsHelloWindowHandleProvider(
                        accessor.Object);

                // Act
                IntPtr result =
                    provider.GetWindowHandle();

                // Assert
                Assert.True(form.IsDisposed);

                Assert.Equal(
                    IntPtr.Zero,
                    result);
            });
    }

    [Fact]
    public void GetWindowHandle_WhenFormHasValidHandle_ReturnsExistingHandle()
    {
        RunOnStaThread(
            () =>
            {
                // Arrange
                using var form = new Form();

                IntPtr expectedHandle =
                    form.Handle;

                Assert.NotEqual(
                    IntPtr.Zero,
                    expectedHandle);

                var accessor =
                    new Mock<IActiveWindowsHelloFormAccessor>();

                accessor
                    .Setup(x => x.GetActiveForm())
                    .Returns(form);

                var provider =
                    new WindowsHelloWindowHandleProvider(
                        accessor.Object);

                // Act
                IntPtr result =
                    provider.GetWindowHandle();

                // Assert
                Assert.Equal(
                    expectedHandle,
                    result);

                Assert.True(
                    form.IsHandleCreated);

                Assert.False(
                    form.IsDisposed);
            });
    }

    [Fact]
    public void GetWindowHandle_WhenActiveFormAccessorChanges_ReturnsCurrentFormHandle()
    {
        RunOnStaThread(
            () =>
            {
                // Arrange
                using var firstForm = new Form();
                using var secondForm = new Form();

                IntPtr firstHandle =
                    firstForm.Handle;

                IntPtr secondHandle =
                    secondForm.Handle;

                Form? activeForm =
                    firstForm;

                var accessor =
                    new Mock<IActiveWindowsHelloFormAccessor>();

                accessor
                    .Setup(x => x.GetActiveForm())
                    .Returns(() => activeForm);

                var provider =
                    new WindowsHelloWindowHandleProvider(
                        accessor.Object);

                // Act
                IntPtr firstResult =
                    provider.GetWindowHandle();

                activeForm =
                    secondForm;

                IntPtr secondResult =
                    provider.GetWindowHandle();

                // Assert
                Assert.Equal(
                    firstHandle,
                    firstResult);

                Assert.Equal(
                    secondHandle,
                    secondResult);

                Assert.NotEqual(
                    firstResult,
                    secondResult);

                accessor.Verify(
                    x => x.GetActiveForm(),
                    Times.Exactly(2));
            });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? capturedException = null;

        var thread =
            new Thread(
                () =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception exception)
                    {
                        capturedException = exception;
                    }
                });

        thread.SetApartmentState(
            ApartmentState.STA);

        thread.Start();
        thread.Join();

        if (capturedException is not null)
        {
            ExceptionDispatchInfo
                .Capture(capturedException)
                .Throw();
        }
    }
}
