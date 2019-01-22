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

        public class GitPackage
        {
            public string CloneUrl;
            public string CloneDir;
            public List<string> ExcludePaths = new List<string>();
        }

        public static ConfigModel FromFile(string path)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            };

            return JsonConvert.DeserializeObject<ConfigModel>(File.ReadAllText(path), settings);
        }
    }
}
