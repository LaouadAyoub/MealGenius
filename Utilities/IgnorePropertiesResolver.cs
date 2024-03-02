using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace MealGeniusBackend.Utilities
{

    public class IgnorePropertiesResolver : DefaultContractResolver
    {
        private readonly HashSet<string> _ignoreProps;

        public IgnorePropertiesResolver(IEnumerable<string> propNamesToIgnore)
        {
            _ignoreProps = new HashSet<string>(propNamesToIgnore, StringComparer.OrdinalIgnoreCase);
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (_ignoreProps.Contains(property.PropertyName))
            {
                property.ShouldSerialize = _ => false;
            }

            return property;
        }
    }

}
