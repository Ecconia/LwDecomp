using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;
using System.Diagnostics;

namespace LwDecomp
{
	internal static class Program
	{
		private static void Main(string[] args)
		{
			if(args.Length != 2)
			{
				exit("Expected two arguments: <game-directory> <output-directory>");
			}
			var gameDirectory = args[0];
			if(!Directory.Exists(gameDirectory))
			{
				exit("Game directory does not exist.");
			}
			var outputDirectory = args[1];
			if(!Directory.Exists(outputDirectory))
			{
				exit("Game directory does not exist.");
			}
			
			var clientFolder = Path.Combine(gameDirectory, "Logic_World_Data", "Managed");
			if(!Directory.Exists(clientFolder))
			{
				exit("Client DLL folder missing.");
			}
			var serverFolder = Path.Combine(gameDirectory, "Server");
			if(!Directory.Exists(serverFolder))
			{
				exit("Server DLL folder missing.");
			}
			
			//Start decompilation:
			
			var skipPrefixes = "Microsoft.;System.;UnityEngine.;Unity.;mscorlib.;netstandard.;WindowsBase.;Newtonsoft.Json.Unity.".Split(";");
			
			Console.WriteLine("\nDecompiling server...");
			DecompDirectory(
				Path.Combine(outputDirectory, "server"), 
				serverFolder, 
				files => files.Where(f => f.EndsWith(".dll") && !skipPrefixes.Any(f.StartsWith))
			);
			
			Console.WriteLine("\nDecompiling client...");
			DecompDirectory(
				Path.Combine(outputDirectory, "client"), 
				clientFolder, 
				files => files.Where(f => f.EndsWith(".dll") && !skipPrefixes.Any(f.StartsWith))
			);
			
			Console.WriteLine("Finished successfully.");
		}
		
		private static void DecompDirectory(string outputFolder, string dllSourceFolder, Func<string[], IEnumerable<string>> decompFileNameSelector)
		{
			var totalRuntime = new Stopwatch();
			totalRuntime.Start();

			if(Directory.Exists(outputFolder))
			{
				Directory.Delete(outputFolder, true);
			}
			Directory.CreateDirectory(outputFolder);
			
			var allFiles = Directory.GetFiles(dllSourceFolder, "*", SearchOption.TopDirectoryOnly)
			                        .Select(x => x.Split(Path.DirectorySeparatorChar).Last())
			                        .ToArray();
			var filesToDecomp = decompFileNameSelector
			                    .Invoke(allFiles)
			                    .Where(x => x.EndsWith(".dll"))
			                    .Select(x => Path.Combine(dllSourceFolder, x));
			
			var ds = new DecompilerSettings(LanguageVersion.Latest);
			var entryRuntime = new Stopwatch();
			foreach (var f in filesToDecomp)
			{
				entryRuntime.Restart();
				
				var decompiledDllName = f.Split(Path.DirectorySeparatorChar).Last();
				Console.Write($"Decompiling {decompiledDllName}... ");
				
				var decProjName = decompiledDllName.Replace(".dll", "") + ".csproj";
				
				var module = new PEFile(f);
				var assemblyResolver = new UniversalAssemblyResolver(f, true, module.DetectTargetFrameworkId());
				var cd = new WholeProjectDecompiler(ds, assemblyResolver, assemblyResolver, null); // maybe add PDB provider
				
				var decOutputFolder = Path.Combine(outputFolder, decompiledDllName);
				Directory.CreateDirectory(decOutputFolder);
				var projectFileName = Path.Combine(decOutputFolder, decProjName);
				using var projectFileWriter = new StreamWriter(File.OpenWrite(projectFileName));
				try
				{
					cd.DecompileProject(module, Path.GetDirectoryName(projectFileName), projectFileWriter);
					entryRuntime.Stop();
				}
				catch(Exception e)
				{
					Console.WriteLine("error!");
					Console.WriteLine("\nError: " + e.Message + "\n");
				}
				finally
				{
					Console.WriteLine($"done in {entryRuntime.ElapsedMilliseconds}ms!");
				}
			}
			
			totalRuntime.Stop();
			Console.WriteLine($"Decompilation done in {totalRuntime.ElapsedMilliseconds}ms!");
		}
		
		private static void exit(string message)
		{
			Console.WriteLine(message);
			Environment.Exit(1);
		}
	}
}
