﻿namespace Ficdown.Console
{
    using System;
    using System.Linq;
    using System.IO;
    using Parser;
    using Parser.Render;
    using Parser.Model.Parser;

    internal class Program
    {
        private static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if(e.ExceptionObject is FicdownException)
                {
                    Console.WriteLine(e.ExceptionObject.ToString());
                    Environment.Exit(3);
                }
            };

            string infile = null;
            string output = null;
            string tempdir = null;
            string format = null;
            string author = null;
            string bookid = null;
            string language = "en";
            string images = null;
            var debug = false;

            if (args.Length == 1)
            {
                if (args[0] == "/?" || args[0] == "/help" || args[0] == "-help" || args[0] == "--help")
                {
                    ShowHelp();
                    return 0;
                }
            }
            else if (args.Length > 1)
            {
                for (var i = 0; i < args.Length; i += 2)
                {
                    switch (args[i])
                    {
                        case "--format":
                            format = args[i + 1];
                            break;
                        case "--in":
                            infile = args[i + 1];
                            break;
                        case "--out":
                            output = args[i + 1];
                            break;
                        case "--template":
                            tempdir = args[i + 1];
                            break;
                        case "--author":
                            author = args[i + 1];
                            break;
                        case "--bookid":
                            bookid = args[i + 1];
                            break;
                        case "--language":
                            language = args[i + 1];
                            break;
                        case "--images":
                            images = args[i + 1];
                            break;
                        case "--debug":
                            i--;
                            debug = true;
                            break;
                        default:
                            Console.WriteLine(@"Unknown option: {0}", args[i]);
                            return 1;
                    }
                }
            }
            else
            {
                ShowHelp();
                return 0;
            }

            Logger.Initialize(debug);
            var logger = Logger.GetLogger<Program>();

            var lintMode = format == "lint";

            if(!lintMode)
            {
                if (string.IsNullOrWhiteSpace(format) || string.IsNullOrWhiteSpace(infile))
                {
                    ShowHelp();
                    return 1;
                }
                if (!File.Exists(infile))
                {
                    logger.Error($"Source file {infile} not found.");
                    return 2;
                }
                if (string.IsNullOrWhiteSpace(output))
                    if (format == "html")
                        output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"html");
                    else if (format == "epub")
                        output = "output.epub";
                    else if(format == "lint")
                        lintMode = true;

                if (!string.IsNullOrWhiteSpace(output) && (Directory.Exists(output) || File.Exists(output)))
                {
                    logger.Error($"Specified output {output} already exists.");
                    return 2;
                }
                if (!string.IsNullOrWhiteSpace(tempdir))
                {
                    if (!Directory.Exists(tempdir))
                    {
                        logger.Error($"Template directory {tempdir} does not exist.");
                        return 2;
                    }
                    if (!File.Exists(Path.Combine(tempdir, "index.html")) ||
                        !File.Exists(Path.Combine(tempdir, "scene.html")) ||
                        !File.Exists(Path.Combine(tempdir, "styles.css")))
                    {
                        logger.Error(
                            @"Template directory must contain ""index.html"", ""scene.html"", and ""style.css"" files.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(images) && !Directory.Exists(images))
                {
                    logger.Error($"Images directory {images} does not exist.");
                    return 2;
                }
            }

            var parser = new FicdownParser();

            string storyText;
            if(!lintMode)
            {
                storyText = File.ReadAllText(infile);
                logger.Log(@"Parsing story...");
            }
            else
            {
                storyText = Console.In.ReadToEnd();
            }

            var story = parser.ParseStory(storyText);

            parser.Warnings.Select(w => w.ToString()).Distinct().ToList().ForEach(s => logger.Raw(s));
            story.Orphans.ToList().ForEach(o => logger.Raw($"Warning L{o.LineNumber},1: \"{o.Name}\": Unreachable {o.Type}"));

            if(!lintMode && parser.Warnings.Count() == 0)
            {
                IRenderer rend;
                switch (format)
                {
                    case "html":
                        Directory.CreateDirectory(output);
                        rend = new HtmlRenderer(language);
                        break;
                    case "epub":
                        if (string.IsNullOrWhiteSpace(author))
                        {
                            logger.Error(@"Epub format requires the --author argument.");
                            return 1;
                        }
                        rend = new EpubRenderer(author, bookid, language);
                        break;
                    default:
                        ShowHelp();
                        return 1;
                }

                if (!string.IsNullOrWhiteSpace(tempdir))
                {
                    rend.IndexTemplate = File.ReadAllText(Path.Combine(tempdir, "index.html"));
                    rend.SceneTemplate = File.ReadAllText(Path.Combine(tempdir, "scene.html"));
                    rend.StylesTemplate = File.ReadAllText(Path.Combine(tempdir, "styles.css"));
                };

                if (!string.IsNullOrWhiteSpace(images)) rend.ImageDir = images;

                logger.Log(@"Rendering story...");

                rend.Render(story, output, debug);

                logger.Log(@"Done.");
            }
            return 0;
        }


        private static void ShowHelp()
        {
            Console.WriteLine(
                @"Usage: ficdown
    --format (html|epub)
    --in ""/path/to/source.md""
    [--out ""/path/to/output""]
    [--template ""/path/to/template/dir""]
    [--images ""/path/to/images/dir""]
    [--author ""Author Name""]
    [--bookid ""ePub Book ID""]
    [--language ""language""]
    [--debug]
or: ficdown --format lint
    (reads input from stdin)");
        }
    }
}
