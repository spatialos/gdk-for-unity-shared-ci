using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace PackageSymLinker
{
    // There are more fields but we don't care about them.
    // Also turns out this is the same for both the manifest.json and the package.json files despite them being 
    // slightly different.
    public class PackageModel
    {
        [JsonProperty("dependencies")]
        public Dictionary<string, string> Dependencies;

        public static PackageModel From(string path)
        {
            return JsonConvert.DeserializeObject<PackageModel>(File.ReadAllText(path));
        }
    }
}
