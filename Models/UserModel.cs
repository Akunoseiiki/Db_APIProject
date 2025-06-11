using System.Text.Json.Serialization;

namespace WSB_project.Models;

public class UserModel
{
    public int IdUser { get; private set; }
    public string Username { get; private set; }
    public string Password { get; private set; }
    public int IdRole { get; private set; }

    [JsonConstructor]
    public UserModel(string username, string password,
        int idRole)
    {
        Username = username;
        Password = password;
        IdRole = idRole;
    }
    
    public UserModel(int idUser, string username, string password,
        int idRole)
    {
        IdUser = idUser;
        Username = username;
        Password = password;
        IdRole = idRole;
    }
}