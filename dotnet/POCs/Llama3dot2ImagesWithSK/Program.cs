using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Error: Please provide the name of the image file as an argument.");
            return;
        }

        string imageName = args[0];
        var imageProcessor = new ImageProcessor();
        await imageProcessor.ProcessImageAsync(imageName);
    }
}
