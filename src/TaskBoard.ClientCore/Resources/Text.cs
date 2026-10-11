using System.Globalization;
using System.Resources;

namespace TaskBoard.ClientCore.Resources;

/// <summary>English fallback and German satellite resources, selected by the device UI culture.</summary>
public static class Text
{
    private static readonly ResourceManager ResourceManager = new("TaskBoard.ClientCore.Resources.Strings", typeof(Text).Assembly);
    public static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    public static string AppTitle => Get(nameof(AppTitle));
    public static string Tasks => Get(nameof(Tasks));
    public static string Diagnostics => Get(nameof(Diagnostics));
    public static string BoardSubtitle => Get(nameof(BoardSubtitle));
    public static string AddTask => Get(nameof(AddTask));
    public static string Refresh => Get(nameof(Refresh));
    public static string Edit => Get(nameof(Edit));
    public static string Delete => Get(nameof(Delete));
    public static string Complete => Get(nameof(Complete));
    public static string Completed => Get(nameof(Completed));
    public static string Incomplete => Get(nameof(Incomplete));
    public static string EmptyTitle => Get(nameof(EmptyTitle));
    public static string EmptyDescription => Get(nameof(EmptyDescription));
    public static string LastSync => Get(nameof(LastSync));
    public static string NeverSynced => Get(nameof(NeverSynced));
    public static string EditorTitle => Get(nameof(EditorTitle));
    public static string TitleLabel => Get(nameof(TitleLabel));
    public static string TitlePlaceholder => Get(nameof(TitlePlaceholder));
    public static string DescriptionLabel => Get(nameof(DescriptionLabel));
    public static string DescriptionPlaceholder => Get(nameof(DescriptionPlaceholder));
    public static string Save => Get(nameof(Save));
    public static string Cancel => Get(nameof(Cancel));
    public static string TitleHint => Get(nameof(TitleHint));
    public static string DeleteTitle => Get(nameof(DeleteTitle));
    public static string DeleteQuestion => Get(nameof(DeleteQuestion));
    public static string DiagnosticsSubtitle => Get(nameof(DiagnosticsSubtitle));
    public static string NormalRequest => Get(nameof(NormalRequest));
    public static string SlowRequest => Get(nameof(SlowRequest));
    public static string TimeoutRequest => Get(nameof(TimeoutRequest));
    public static string FailureRequest => Get(nameof(FailureRequest));
    public static string Duration => Get(nameof(Duration));
    public static string Connected => Get(nameof(Connected));
    public static string Disconnected => Get(nameof(Disconnected));
    public static string ConnectionHint => Get(nameof(ConnectionHint));
    public static string DevelopmentOnly => Get(nameof(DevelopmentOnly));
    public static string RequestSucceeded => Get(nameof(RequestSucceeded));
    public static string RequestFailed => Get(nameof(RequestFailed));
    public static string RequestTimedOut => Get(nameof(RequestTimedOut));
    public static string RequestCanceled => Get(nameof(RequestCanceled));
    public static string ValidationError => Get(nameof(ValidationError));
    public static string UnexpectedError => Get(nameof(UnexpectedError));
    public static string InvalidApiResponse => Get(nameof(InvalidApiResponse));
    public static string ServiceTemporarilyUnavailable => Get(nameof(ServiceTemporarilyUnavailable));
    public static string StartupErrorTitle => Get(nameof(StartupErrorTitle));
    public static string StartupErrorMessage => Get(nameof(StartupErrorMessage));
}
