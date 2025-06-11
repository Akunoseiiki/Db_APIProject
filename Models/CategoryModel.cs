using System.Text.Json.Serialization;

namespace WSB_project.Models;

public class CategoryModel
{
        public int IdCategory { get; private set; }
        public string Name { get; private set; }

        [JsonConstructor]
        public CategoryModel(string name)
        {
            Name = name;
        }
            
        public CategoryModel(int idcategory, string name)
        {
            IdCategory = idcategory;
            Name = name;
        }
}