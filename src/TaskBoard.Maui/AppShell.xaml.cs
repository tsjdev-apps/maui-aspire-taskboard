using TaskBoard.Maui.Views;

namespace TaskBoard.Maui;

public partial class AppShell : Shell
{
    public AppShell(TasksPage tasks, DiagnosticsPage diagnostics)
    {
        InitializeComponent();
        TasksContent.Content = tasks;
        DiagnosticsContent.Content = diagnostics;
        Routing.RegisterRoute("editor", typeof(TaskEditorPage));
    }
}
