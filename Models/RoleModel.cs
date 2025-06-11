using System.Text.Json.Serialization;

namespace WSB_project.Models;

public class RoleModel
{
    public int IdRole { get; private set; }
    public string Name { get; private set; }
    
    [JsonConstructor]
    public RoleModel(string name)
    {
        Name = name;
    }
    
    public RoleModel(int idrole, string name)
    {
        IdRole = idrole;
        Name = name;
    }
}