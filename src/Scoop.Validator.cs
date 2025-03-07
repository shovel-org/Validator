using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Scoop {
    public class JsonParserException : Exception {
        public string FileName { get; set; }
        public JsonParserException(string file, string message) : base(message) { this.FileName = file; }
        public JsonParserException(string file, string message, Exception inner) : base(message, inner) { this.FileName = file; }
    }

    public class Validator {
        private bool CI { get; set; }
        public JSchema Schema { get; private set; }
        public FileInfo SchemaFile { get; private set; }
        public JObject Manifest { get; private set; }
        public FileInfo ManifestFile { get; private set; }
        public IList<string> Errors { get; private set; }
        public string ErrorsAsString {
            get {
                return String.Join(System.Environment.NewLine, this.Errors);
            }
        }

        private JSchema ParseSchema(string file) {
            try {
                return JSchema.Parse(File.ReadAllText(file, System.Text.Encoding.UTF8));
            } catch (Newtonsoft.Json.JsonReaderException e) {
                throw new JsonParserException(Path.GetFileName(file), e.Message, e);
            } catch (FileNotFoundException e) {
                throw e;
            }
        }

        private JObject ParseManifest(string file) {
            var isYaml = false;
            if (file.EndsWith(".yml") || file.EndsWith(".yaml")) {
                isYaml = true;
                object yamlObject;
                using (var r = new StreamReader(file)) {
                    yamlObject = new DeserializerBuilder()
                        .WithNodeTypeResolver(new InferTypeFromValue())
                        .Build()
                        .Deserialize(r);
                }

                var js = new SerializerBuilder()
                    .JsonCompatible()
                    .Build();

                file = $"{file}-{new Random().Next(258, 258258)}.json";
                using (StreamWriter writer = new StreamWriter(file)) {
                    js.Serialize(writer, yamlObject);
                }
            }

            try {
                return JObject.Parse(File.ReadAllText(file, System.Text.Encoding.UTF8));
            } catch (Newtonsoft.Json.JsonReaderException e) {
                throw new JsonParserException(Path.GetFileName(file), e.Message, e);
            } catch (FileNotFoundException e) {
                throw e;
            } finally {
                if (isYaml) { File.Delete(file); }
            }
        }

        public Validator(string schemaFile) {
            this.SchemaFile = new FileInfo(schemaFile);
            this.Errors = new List<string>();
        }

        public Validator(string schemaFile, bool ci) {
            this.SchemaFile = new FileInfo(schemaFile);
            this.Errors = new List<string>();
            this.CI = ci;
        }

        public bool Validate(string file) {
            this.ManifestFile = new FileInfo(file);
            return this.Validate();
        }

        public bool Validate() {
            if (!this.SchemaFile.Exists) {
                Console.WriteLine("ERROR: Please provide schema.json!");
                return false;
            }
            if (!this.ManifestFile.Exists) {
                Console.WriteLine("ERROR: Please provide manifest file!");
                return false;
            }

            this.Errors.Clear();
            try {
                if (this.Schema == null) { this.Schema = this.ParseSchema(this.SchemaFile.FullName); }
                this.Manifest = this.ParseManifest(this.ManifestFile.FullName);
            } catch (FileNotFoundException e) {
                this.Errors.Add(e.Message);
            } catch (JsonParserException e) {
                this.Errors.Add(String.Format("{0}{1}: {2}", (this.CI ? "    [*] " : ""), e.FileName, e.Message));
            }

            if (this.Schema == null || this.Manifest == null) { return false; }

            IList<ValidationError> validationErrors = new List<ValidationError>();
            this.Manifest.IsValid(this.Schema, out validationErrors);

            if (validationErrors.Count == 0) { return true; }

            traverseErrors(validationErrors, this.CI ? 3 : 1);

            return (this.Errors.Count == 0);
        }

        public void traverseErrors(IList<ValidationError> errors, int level = 1) {
            if (errors == null) { return; }

            foreach (ValidationError error in errors) {
                StringBuilder sb = new StringBuilder();
                sb.Insert(sb.Length, " ", level * 2);
                sb.Insert(sb.Length, this.CI ? "[*] " : "- ");
                sb.AppendFormat("Error: {0}\n", error.Message);

                sb.Insert(sb.Length, " ", level * 2);
                sb.Insert(sb.Length, this.CI ? "  [^] " : "  ");
                sb.AppendFormat("Line: {0}:{1}:{2}\n", this.ManifestFile.FullName, error.LineNumber, error.LinePosition);

                sb.Insert(sb.Length, " ", level * 2);
                sb.Insert(sb.Length, this.CI ? "  [^] " : "  ");
                sb.AppendFormat("Path: {0}/{1}", error.SchemaId, error.ErrorType);

                if (!this.CI) { sb.Insert(sb.Length, "\n"); }

                this.Errors.Add(sb.ToString());

                if (error.ChildErrors != null || error.ChildErrors.Count > 0) {
                    traverseErrors(error.ChildErrors, level + 1);
                }
            }
        }
    }

    // Helper class for workaround of boolean handling
    public class InferTypeFromValue : INodeTypeResolver {
        public bool Resolve(NodeEvent nodeEvent, ref Type currentType) {
            // https://yaml.org/type/bool.html
            string[] yamlBool = {
                "y",
                "Y",
                "yes",
                "Yes",
                "YES",

                "n",
                "N",
                "no",
                "No",
                "NO",

                "true",
                "True",
                "TRUE",
                "false",
                "False",
                "FALSE",
                "on",
                "On",
                "ON",
                "off",
                "Off",
                "OFF"
            };
            var scalar = nodeEvent as Scalar;

            if (scalar != null) {
                if (Array.Exists(yamlBool, (v) => { return v == scalar.Value; })) {
                    currentType = typeof(bool);
                    return true;
                }
            }

            return false;
        }
    }
}
