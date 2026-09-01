using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Windows.Input;

namespace LibationAvalonia.DesignSystem.Components;

public partial class BookCard : UserControl
{
	public static readonly StyledProperty<IImage?> CoverProperty = AvaloniaProperty.Register<BookCard, IImage?>(nameof(Cover));
	public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<BookCard, string?>(nameof(Title));
	public static readonly StyledProperty<string?> AuthorProperty = AvaloniaProperty.Register<BookCard, string?>(nameof(Author));
	public static readonly StyledProperty<string?> NarratorProperty = AvaloniaProperty.Register<BookCard, string?>(nameof(Narrator));
	public static readonly StyledProperty<string?> DurationProperty = AvaloniaProperty.Register<BookCard, string?>(nameof(Duration));
	public static readonly StyledProperty<LibationStatusKind> StatusProperty = AvaloniaProperty.Register<BookCard, LibationStatusKind>(nameof(Status), LibationStatusKind.Downloaded);
	public static readonly StyledProperty<string?> StatusTextProperty = AvaloniaProperty.Register<BookCard, string?>(nameof(StatusText));
	public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<BookCard, bool>(nameof(IsSelected));
	public static readonly StyledProperty<bool> ShowProgressProperty = AvaloniaProperty.Register<BookCard, bool>(nameof(ShowProgress));
	public static readonly StyledProperty<double> ProgressProperty = AvaloniaProperty.Register<BookCard, double>(nameof(Progress));
	public static readonly StyledProperty<bool> ShowOpenActionProperty = AvaloniaProperty.Register<BookCard, bool>(nameof(ShowOpenAction), true);
	public static readonly StyledProperty<ICommand?> CommandProperty = AvaloniaProperty.Register<BookCard, ICommand?>(nameof(Command));
	public static readonly StyledProperty<object?> CommandParameterProperty = AvaloniaProperty.Register<BookCard, object?>(nameof(CommandParameter));
	public static readonly StyledProperty<ICommand?> ContextCommandProperty = AvaloniaProperty.Register<BookCard, ICommand?>(nameof(ContextCommand));
	public static readonly StyledProperty<object?> ContextCommandParameterProperty = AvaloniaProperty.Register<BookCard, object?>(nameof(ContextCommandParameter));

	public BookCard()
	{
		InitializeComponent();
		UpdateSelectedState();
	}

	public IImage? Cover { get => GetValue(CoverProperty); set => SetValue(CoverProperty, value); }
	public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
	public string? Author { get => GetValue(AuthorProperty); set => SetValue(AuthorProperty, value); }
	public string? Narrator { get => GetValue(NarratorProperty); set => SetValue(NarratorProperty, value); }
	public string? Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
	public LibationStatusKind Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
	public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
	public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
	public bool ShowProgress { get => GetValue(ShowProgressProperty); set => SetValue(ShowProgressProperty, value); }
	public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
	public bool ShowOpenAction { get => GetValue(ShowOpenActionProperty); set => SetValue(ShowOpenActionProperty, value); }
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
