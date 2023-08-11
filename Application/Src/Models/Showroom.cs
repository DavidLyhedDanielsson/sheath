namespace Application.Models;

public class Showroom
{
    public class Showcase
    {
        public required Model Model { get; init; }
    }

    private Dictionary<string, Showcase> _showcases = new();

    public Showroom() { }

    public void AddShowcase(string name, Model model)
    {
        _showcases.Add(name, new Showcase { Model = model });
    }

    public Showcase? GetShowcase(string name)
    {
        _showcases.TryGetValue(name, out Showcase? showcase);
        return showcase;
    }
}