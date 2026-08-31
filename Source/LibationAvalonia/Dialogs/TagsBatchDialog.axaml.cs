using Avalonia.Controls;

namespace LibationAvalonia.Dialogs;

public partial class TagsBatchDialog : DialogWindow
{
	public string? NewTags { get; set; }
	public string DialogTitle { get; }
	public string InstructionText { get; }
	public string SaveButtonText { get; }

	public TagsBatchDialog() : this(addTags: false) { }

	public TagsBatchDialog(bool addTags)
	{
		DialogTitle = addTags ? "Add Tags" : "Replace Tags";
		InstructionText = addTags
			? "Tags are separated by a space. Existing tags will be kept."
			: "Tags are separated by a space. Each tag can contain letters, numbers, and underscores.";
		SaveButtonText = addTags ? "Add" : "Replace";
		InitializeComponent();
		ControlToFocusOnShow = this.FindControl<TextBox>(nameof(EditTagsTb));

		DataContext = this;
	}

	// For compiled bindings
	public new void SaveAndClose() => base.SaveAndClose();
}
