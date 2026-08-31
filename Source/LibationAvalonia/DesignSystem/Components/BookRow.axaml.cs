using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public partial class BookRow : UserControl
{
	public static readonly StyledProperty<IImage?> CoverProperty = AvaloniaProperty.Register<BookRow, IImage?>(nameof(Cover));
	public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<BookRow, string?>(nameof(Title));
	public static readonly StyledProperty<string?> SupportingTextProperty = AvaloniaProperty.Register<BookRow, string?>(nameof(SupportingText));
	public static readonly StyledProperty<string?> MetadataProperty = AvaloniaProperty.Register<BookRow, string?>(nameof(Metadata));
	public static readonly StyledProperty<LibationStatusKind> StatusProperty = AvaloniaProperty.Register<BookRow, LibationStatusKind>(nameof(Status), LibationStatusKind.Downloaded);
	public static readonly StyledProperty<string?> StatusTextProperty = AvaloniaProperty.Register<BookRow, string?>(nameof(StatusText));
	public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<BookRow, bool>(nameof(IsSelected));
	public static readonly StyledProperty<bool> ShowProgressProperty = AvaloniaProperty.Register<BookRow, bool>(nameof(ShowProgress));
	public static readonly StyledProperty<double> ProgressProperty = AvaloniaProperty.Register<BookRow, double>(nameof(Progress));
	public static readonly StyledProperty<ICommand?> CommandProperty = AvaloniaProperty.Register<BookRow, ICommand?>(nameof(Command));
	public static readonly StyledProperty<object?> CommandParameterProperty = AvaloniaProperty.Register<BookRow, object?>(nameof(CommandParameter));
	public static readonly StyledProperty<ICommand?> ContextCommandProperty = AvaloniaProperty.Register<BookRow, ICommand?>(nameof(ContextCommand));
	public static readonly StyledProperty<object?> ContextCommandParameterProperty = AvaloniaProperty.Register<BookRow, object?>(nameof(ContextCommandParameter));

	public BookRow()
	{
		InitializeComponent();
		UpdateSelectedState();
	}

	public IImage? Cover { get => GetValue(CoverProperty); set => SetValue(CoverProperty, value); }
	public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
	public string? SupportingText { get => GetValue(SupportingTextProperty); set => SetValue(SupportingTextProperty, value); }
	public string? Metadata { get => GetValue(MetadataProperty); set => SetValue(MetadataProperty, value); }
	public LibationStatusKind Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
	public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
	public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
	public bool ShowProgress { get => GetValue(ShowProgressProperty); set => SetValue(ShowProgressProperty, value); }
	public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
	public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
	public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
	public ICommand? ContextCommand { get => GetValue(ContextCommandProperty); set => SetValue(ContextCommandProperty, value); }
	public object? ContextCommandParameter { get => GetValue(ContextCommandParameterProperty); set => SetValue(ContextCommandParameterProperty, value); }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == IsSelectedProperty)
			UpdateSelectedState();
	}

	private void UpdateSelectedState() => PseudoClasses.Set(":selected", IsSelected);
}
