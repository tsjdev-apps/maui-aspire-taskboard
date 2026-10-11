using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskBoard.ClientCore.Resources;

namespace TaskBoard.Maui;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly AppStartupState _startup;
    private readonly ILogger<App> _logger;

    public App(IServiceProvider services, AppStartupState startup, ILogger<App> logger)
    {
        _services = services;
        _startup = startup;
        _logger = logger;
        try
        {
            InitializeComponent();
        }
        catch (Exception exception)
        {
            _startup.Error ??= exception;
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        if (_startup.Error is null)
        {
            try
            {
                return new Window(_services.GetRequiredService<AppShell>()) { Title = Text.AppTitle };
            }
            catch (Exception exception)
            {
                _startup.Error = exception;
            }
        }

        _logger.LogError(_startup.Error, "Native application initialization failed");
        var page = new ContentPage
        {
            Title = Text.StartupErrorTitle,
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 24,
                    Spacing = 16,
                    Children =
                    {
                        new Label { Text = Text.StartupErrorTitle, FontSize = 24 },
                        new Label { Text = Text.StartupErrorMessage, LineBreakMode = LineBreakMode.WordWrap }
                    }
                }
            }
        };
        return new Window(page) { Title = $"{Text.AppTitle} - {Text.StartupErrorTitle}" };
    }
}
