using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Test
{
    /// <summary>
    /// Serialization helper.
    /// </summary>
    public static class SerializationHelper
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private static ExceptionConverter<Exception> _ExceptionConverter = new ExceptionConverter<Exception>();
        private static NameValueCollectionConverter _NameValueCollectionConverter = new NameValueCollectionConverter();
        private static JsonStringEnumConverter _StringEnumConverter = new JsonStringEnumConverter();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Serialize JSON.
        /// </summary>
        /// <param name="obj">Object.</param>
        /// <param name="pretty">Pretty print.</param>
        /// <returns>JSON string.</returns>
        public static string SerializeJson(object obj, bool pretty = true)
        {
            if (obj == null) return null;

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

            // see https://github.com/dotnet/runtime/issues/43026
            options.Converters.Add(_ExceptionConverter);
            options.Converters.Add(_NameValueCollectionConverter);
            options.Converters.Add(_StringEnumConverter);

            if (!pretty)
            {
                options.WriteIndented = false;
                return JsonSerializer.Serialize(obj, options);
            }
            else
            {
                options.WriteIndented = true;
                return JsonSerializer.Serialize(obj, options);
            }
        }

        /// <summary>
        /// Deserialize JSON. 
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>Instance.</returns>
        public static T DeserializeJson<T>(string json)
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.AllowTrailingCommas = true;
            options.ReadCommentHandling = JsonCommentHandling.Skip;

            options.Converters.Add(_ExceptionConverter);
            options.Converters.Add(_NameValueCollectionConverter);
            options.Converters.Add(_StringEnumConverter);
            return JsonSerializer.Deserialize<T>(json, options);
        }

        #endregion

        #region Private-Methods

        private class ExceptionConverter<TExceptionType> : JsonConverter<TExceptionType>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(Exception).IsAssignableFrom(typeToConvert);
            }

            public override TExceptionType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException("Deserializing exceptions is not allowed");
            }

            public override void Write(Utf8JsonWriter writer, TExceptionType value, JsonSerializerOptions options)
            {
                IEnumerable<PropertyInfo> properties = value.GetType()
                    .GetProperties()
                    .Where(uu => uu.Name != nameof(Exception.TargetSite));

#pragma warning disable SYSLIB0020
                if (options?.IgnoreNullValues == true
                    || options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull)
                {
                    properties = properties.Where(uu => uu.GetValue(value) != null);
                }
#pragma warning restore SYSLIB0020

                List<PropertyInfo> propList = properties.ToList();

                if (propList.Count == 0)
                {
                    // Nothing to write
                    return;
                }

                writer.WriteStartObject();

                foreach (PropertyInfo prop in propList)
                {
                    writer.WritePropertyName(prop.Name);
                    JsonSerializer.Serialize(writer, prop.GetValue(value), options);
                }

                writer.WriteEndObject();
            }
        }

        private class NameValueCollectionConverter : JsonConverter<NameValueCollection>
        {
            public override NameValueCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

            public override void Write(Utf8JsonWriter writer, NameValueCollection value, JsonSerializerOptions options)
            {
                Dictionary<string, string> dict = value.Keys.Cast<string>()
                    .ToDictionary(k => k, k => string.Join(", ", value.GetValues(k)));
                System.Text.Json.JsonSerializer.Serialize(writer, dict);
            }
        }

        #endregion
    }
}
