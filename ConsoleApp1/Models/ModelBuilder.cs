using ConsoleApp1.Asset;

namespace ConsoleApp1.Models;

public class ModelBuilder
{
   public static Model CreateModel(Mesh mesh)
   {
      return new Model();
   }

   public static Material CreateMaterial()
   {
      return new Material();
   }
}