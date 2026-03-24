using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Kokuban;

namespace XCom2ModCompiler.Utilities;

public class ProjectSynchronizer
{
    private readonly ILogger<ProjectSynchronizer> _logger;

    public ProjectSynchronizer(ILogger<ProjectSynchronizer> logger)
    {
        _logger = logger;
    }

    public virtual void Synchronize(string x2projPath)
    {
        if (!File.Exists(x2projPath))
        {
            Console.WriteLine(Chalk.Gray[$"Project file {x2projPath} not found. Skipping synchronization."]);
            return;
        }

        var projectRoot = Path.GetDirectoryName(x2projPath)!;
        var x2projFilename = Path.GetFileName(x2projPath);

        XDocument doc = XDocument.Load(x2projPath);
        XNamespace ns = doc.Root!.GetDefaultNamespace();

        // Remove all existing ItemGroups that contain Folder, Content, or None
        Console.WriteLine($"{Chalk.Cyan["Clearing"]} old ItemGroups...");
        var itemGroupsToRemove = doc.Descendants(ns + "ItemGroup")
            .Where(ig => ig.Elements(ns + "Folder").Any() ||
                         ig.Elements(ns + "Content").Any() ||
                         ig.Elements(ns + "None").Any())
            .ToList();

        foreach (var ig in itemGroupsToRemove)
        {
            foreach (var child in ig.Nodes())
            {
                if (child is XElement element)
                {
                    var includeValue = element.Attribute("Include")?.Value;
                    if (includeValue != null)
                    {
                        var elementType = element.Name.LocalName;
                        var color = elementType == "Folder" ? Chalk.Magenta : Chalk.White;
                        Console.WriteLine($"{Chalk.Yellow["Removed"]} {color[elementType]} {Chalk.Gray["'"]}{Chalk.White[includeValue]}{Chalk.Gray["'"]} from ItemGroups");
                    }
                }
            }
            ig.Remove();
        }

        Console.WriteLine($"{Chalk.Cyan["Scanning"]} project directory for files and folders...");

        // Gather all files recursively
        var allFilesList = new List<string>();
        foreach (var file in Directory.GetFiles(projectRoot, "*.*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(projectRoot, file);
            if (!relativePath.Equals(x2projFilename, StringComparison.OrdinalIgnoreCase) &&
                !relativePath.StartsWith("ContentForCook", StringComparison.OrdinalIgnoreCase) &&
                !relativePath.StartsWith("BuildCache", StringComparison.OrdinalIgnoreCase) &&
                !relativePath.StartsWith(".scripts", StringComparison.OrdinalIgnoreCase) &&
                !relativePath.StartsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                allFilesList.Add(relativePath);
            }
        }

        var allFiles = allFilesList.ToArray();

        // Gather all unique folders
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in allFiles)
        {
            var dir = Path.GetDirectoryName(file);
            while (!string.IsNullOrEmpty(dir))
            {
                folders.Add(dir);
                dir = Path.GetDirectoryName(dir);
            }
        }

        var fileCount = allFiles.Length;
        var folderCount = folders.Count;
        Console.WriteLine($"{Chalk.Cyan["Found"]} {Chalk.White[fileCount.ToString()]} files and {Chalk.White[folderCount.ToString()]} folders.");

        var newItemGroup = new XElement(ns + "ItemGroup");

        // Add Folder elements (sorted) - use standard LINQ for HashSet (small collection)
        foreach (var folder in folders.ToList().OrderBy(f => f))
        {
            newItemGroup.Add(new XElement(ns + "Folder", new XAttribute("Include", folder)));
        }

        // Add Content elements (sorted)
        foreach (var file in allFiles.OrderBy(f => f))
        {
            newItemGroup.Add(new XElement(ns + "Content", new XAttribute("Include", file)));
        }

        Console.WriteLine($"{Chalk.Cyan["Updating"]} ItemGroup with folders and files...");

        // Sort nodes first by Name ascending (Folder before Content), then by Include ascending
        var sortedNodes = newItemGroup.Nodes().OfType<XElement>()
            .OrderBy(n => n.Name.LocalName)
            .ThenBy(n => n.Attribute("Include")?.Value)
            .ToList();

        foreach (var node in sortedNodes)
        {
            var includeValue = node.Attribute("Include")?.Value;
            var elementType = node.Name.LocalName;
            var color = elementType == "Folder" ? Chalk.Magenta : Chalk.White;
            Console.WriteLine($"{Chalk.Blue["Added"]} {color[elementType]} {Chalk.Gray["'"]}{Chalk.White[includeValue]}{Chalk.Gray["'"]} to ItemGroups");
        }

        doc.Root.Add(newItemGroup);
        doc.Save(x2projPath);

        Console.WriteLine($"{Chalk.Green["ItemGroup regeneration completed successfully"]} for project {Chalk.Cyan[Path.GetFileNameWithoutExtension(x2projPath)]}.");
    }
}
