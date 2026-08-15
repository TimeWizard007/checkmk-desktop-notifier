using CheckmkDesktopNotifier.App.ViewModels;
using CheckmkDesktopNotifier.App.Views;
using System.ComponentModel;
using System.Windows;

namespace CheckmkDesktopNotifier.App;

public sealed class UiShell
{
    private readonly CompactBarWindow _bar;
    private readonly ProblemListWindow _list;
    private readonly ShellViewModel _viewModel;
    private readonly WindowSessionState _session;

    public UiShell(
        CompactBarWindow bar,
        ProblemListWindow list,
        ShellViewModel viewModel,
        WindowSessionState session)
    {
        _bar = bar;
        _list = list;
        _viewModel = viewModel;
        _session = session;

        _bar.DataContext = _viewModel;
        _list.DataContext = _viewModel;

        _bar.LocationChanged += (_, _) =>
        {
            _session.BarLeft = _bar.Left;
            _session.BarTop = _bar.Top;
            PositionList();
        };

        _bar.SizeChanged += (_, _) => PositionList();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _list.Closing += OnListClosing;
        _bar.Closed += (_, _) => Application.Current.Shutdown();
    }

    public void Show()
    {
        RestoreOrPlaceBar();
        _bar.Show();
        AttachListOwner();
        PositionList();
        ApplyExpandedState();
    }

    private void AttachListOwner()
    {
        if (!_bar.IsLoaded && !_bar.IsVisible)
        {
            return;
        }

        if (!ReferenceEquals(_list.Owner, _bar))
        {
            _list.Owner = _bar;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.IsExpanded) or null)
        {
            ApplyExpandedState();
        }
    }

    private void ApplyExpandedState()
    {
        if (_viewModel.IsExpanded)
        {
            AttachListOwner();
            PositionList();
            if (!_list.IsVisible)
            {
                _list.Show();
            }

            _list.Activate();
            return;
        }

        if (_list.IsVisible)
        {
            _list.Hide();
        }
    }

    private void OnListClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        _viewModel.IsExpanded = false;
    }

    private void RestoreOrPlaceBar()
    {
        if (_session.BarLeft is { } left && _session.BarTop is { } top)
        {
            _bar.Left = left;
            _bar.Top = top;
            return;
        }

        _bar.WindowStartupLocation = WindowStartupLocation.Manual;
        var work = SystemParameters.WorkArea;
        _bar.Left = Math.Max(work.Left, work.Right - 720);
        _bar.Top = work.Top + 12;
        _session.BarLeft = _bar.Left;
        _session.BarTop = _bar.Top;
    }

    private void PositionList()
    {
        const double gap = 6;
        var work = SystemParameters.WorkArea;
        var listHeight = _list.ActualHeight > 0 ? _list.ActualHeight : _list.Height;
        var below = _bar.Top + _bar.ActualHeight + gap;
        var above = _bar.Top - listHeight - gap;
        var top = below + listHeight <= work.Bottom ? below : Math.Max(work.Top, above);

        _list.Left = Math.Min(_bar.Left, work.Right - _list.Width);
        _list.Top = top;
    }
}
