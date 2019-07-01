using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Packer
{
    public class ConfigModel
    {
        public string PackageName;
        public string Version;
        public List<GitPackage> GitPackages = new List<GitPackage>();

        private static JsonSerializerSettings settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
            Formatting = Formatting.Indented
        };

        public class GitPackage
        {
            public string CloneUrl;
            public string CloneDir;
            public List<string> ExcludePaths = new List<string>();
        }

        public static ConfigModel FromFile(string path)
        {
            return JsonConvert.DeserializeObject<ConfigModel>(File.ReadAllText(path), settings);
        }

        public void ToFile(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, settings));
        }
    }
}
