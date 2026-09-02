using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using DataLayer;
using LibationUiBase.GridView;
using System;

namespace LibationAvalonia.Views;

public partial class LiberateStatusButton : UserControl
{
	public event EventHandler? Click;

	public static readonly StyledProperty<LiberatedStatus> BookStatusProperty =
	AvaloniaProperty.Register<LiberateStatusButton, LiberatedStatus>(nameof(BookStatus));

	public static readonly StyledProperty<LiberatedStatus?> PdfStatusProperty =
	AvaloniaProperty.Register<LiberateStatusButton, LiberatedStatus?>(nameof(PdfStatus));

	public static readonly StyledProperty<bool> IsUnavailableProperty =
	AvaloniaProperty.Register<LiberateStatusButton, bool>(nameof(IsUnavailable));
	public static readonly StyledProperty<bool> IsSeriesProperty =
	AvaloniaProperty.Register<LiberateStatusButton, bool>(nameof(IsSeries));
	public static readonly StyledProperty<bool> IsExpandedProperty =
	AvaloniaProperty.Register<LiberateStatusButton, bool>(nameof(IsExpanded));
	public static readonly StyledProperty<bool> UseVectorStatusProperty =
	AvaloniaProperty.Register<LiberateStatusButton, bool>(nameof(UseVectorStatus));
	public static readonly StyledProperty<string?> StatusTextProperty =
	AvaloniaProperty.Register<LiberateStatusButton, string?>(nameof(StatusText));
	public static readonly DirectProperty<LiberateStatusButton, bool> IsButtonEnabledProperty =
	AvaloniaProperty.RegisterDirect<LiberateStatusButton, bool>(nameof(IsButtonEnabled), control => control.IsButtonEnabled);

	public static readonly StyledProperty<IImage?> ButtonImageProperty =
	AvaloniaProperty.Register<LiberateStatusButton, IImage?>(nameof(ButtonImage));

	public LiberatedStatus BookStatus { get => GetValue(BookStatusProperty); set => SetValue(BookStatusProperty, value); }
	public LiberatedStatus? PdfStatus { get => GetValue(PdfStatusProperty); set => SetValue(PdfStatusProperty, value); }
	public bool IsUnavailable { get => GetValue(IsUnavailableProperty); set => SetValue(IsUnavailableProperty, value); }
	public bool IsSeries { get => GetValue(IsSeriesProperty); set => SetValue(IsSeriesProperty, value); }
	public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
	public bool UseVectorStatus { get => GetValue(UseVectorStatusProperty); set => SetValue(UseVectorStatusProperty, value); }
	public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
	public bool IsButtonEnabled
	{
		get;
		private set => SetAndRaise(IsButtonEnabledProperty, ref field, value);
	} = true;

	/// <summary>The shared rendering of this entry's status, from <see cref="EntryStatus.ButtonImage"/>.</summary>
	public IImage? ButtonImage { get => GetValue(ButtonImageProperty); set => SetValue(ButtonImageProperty, value); }

	public LiberateStatusButton()
	{
		InitializeComponent();
		DataContextChanged += LiberateStatusButton_DataContextChanged;

		//The icon is rendered for a specific theme, so it has to be re-rendered when the theme changes.
		ActualThemeVariantChanged += (_, _) =>
		{
			if (!UseVectorStatus)
				(DataContext as GridEntry)?.Liberate?.Invalidate(nameof(EntryStatus.ButtonImage));
		};
		UpdatePresentation();
	}

	private void LiberateStatusButton_DataContextChanged(object? sender, EventArgs e)
	{
		//Force book status recheck when an entry is scrolled into view.
		//This will force a recheck for a partially downloaded file.
		var status = DataContext as LibraryBookEntry;
		status?.Liberate?.Invalidate(nameof(status.Liberate.BookStatus), nameof(status.Liberate.ButtonImage));
	}

	private void Button_Click(object sender, RoutedEventArgs e) => Click?.Invoke(this, EventArgs.Empty);

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == BookStatusProperty
			|| change.Property == PdfStatusProperty
			|| change.Property == IsUnavailableProperty
			|| change.Property == IsSeriesProperty
			|| change.Property == IsExpandedProperty)
			UpdatePresentation();
	}

	private void UpdatePresentation()
	{
		IsButtonEnabled = IsSeries || (BookStatus is not LiberatedStatus.Error
			&& (!IsUnavailable || (BookStatus is LiberatedStatus.Liberated && PdfStatus is null or LiberatedStatus.Liberated)));

		foreach (var state in new[]
		{
			":download-pending", ":downloading", ":downloaded", ":failed",
			":unavailable", ":series-collapsed", ":series-expanded",
		})
			PseudoClasses.Set(state, false);

		var active = IsSeries
			? IsExpanded ? ":series-expanded" : ":series-collapsed"
			: IsUnavailable ? ":unavailable"
			: BookStatus switch
			{
				LiberatedStatus.Liberated => ":downloaded",
				LiberatedStatus.PartialDownload => ":downloading",
				LiberatedStatus.Error => ":failed",
				_ => ":download-pending",
			};
		PseudoClasses.Set(active, true);
	}
}
