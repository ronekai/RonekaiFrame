using RonekaiImageFramer.Templates;

namespace RonekaiImageFramer.Models;

public sealed class TemplateListItem(IProductTemplate template)
{
    public IProductTemplate Template { get; } = template;

    public string Name =>
        $"{Template.Name} ({Template.OutputSize.Width}×{Template.OutputSize.Height} px)";

    public string SizeLabel => $"{Template.OutputSize.Width} × {Template.OutputSize.Height} px";

    public string Description => Template.Description;
}
