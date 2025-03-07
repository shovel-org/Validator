using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scoop {
    public class Program {
        public static int Main(string[] args) {
            if (args.Length < 2) {
                Console.WriteLine("Usage: validator.exe <SCHEMA> <MANIFEST>...");
                return 1;
            }

            bool ci = String.Format("{0}", Environment.GetEnvironmentVariable("CI")).ToLower() == "true";
            bool valid = true;

            IList<string> manifests = args.ToList<String>();
            String schema = manifests.First();
            manifests.RemoveAt(0);
            String combinedArgs = String.Join("", manifests);

            if (combinedArgs.Contains("*") || combinedArgs.Contains("?")) {
                try {
                    var path = new Uri(Path.Combine(Directory.GetCurrentDirectory(), combinedArgs)).LocalPath;
                    var drive = Path.GetPathRoot(path);
                    var pattern = path.Replace(drive, "");
                    manifests = Directory.GetFiles(drive, pattern).ToList<String>();
                } catch (System.ArgumentException ex) {
                    Console.WriteLine("Invalid path provided! ({0})", ex.Message);
                    return 1;
                }
            }

            Scoop.Validator validator = new Scoop.Validator(schema, ci);

            foreach (var manifest in manifests) {
                if (validator.Validate(manifest)) {
                    var prefix = ci ? "      [+]" : "-";
                    Console.WriteLine("{0} {1} validates against the schema!", prefix, Path.GetFileName(manifest));
                } else {
                    var prefix = ci ? "      [-]" : "-";
                    Console.WriteLine(
                        "{0} {1} has {2} Error{3}",
                        prefix,
                        Path.GetFileName(manifest),
                        validator.Errors.Count,
                        validator.Errors.Count > 1 ? "s" : ""
                    );

                    valid = false;

                    foreach (var error in validator.Errors) {
                        Console.WriteLine(error);
                    }
                }
            }

            return valid ? 0 : 1;
        }
    }
}
