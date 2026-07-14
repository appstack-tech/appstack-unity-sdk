using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace Appstack.Tests
{
    public sealed class AppstackPublicApiSnapshotTests
    {
        private const string ExpectedSnapshot =
            "public static class Appstack.AppstackSDK\n" +
            "  void Configure(string apiKey, int logLevel = 1, string customerUserId = null)\n" +
            "  void SendEvent(EventType eventType, string eventName = null, " +
            "Dictionary<string, object> parameters = null)\n" +
            "  void EnableAppleAdsAttribution()\n" +
            "  string GetAppstackId()\n" +
            "  bool IsSdkDisabled()\n" +
            "  void GetAttributionParams(Action<Dictionary<string, object>> onSuccess, " +
            "Action<string> onError = null)\n" +
            "public enum Appstack.EventType\n" +
            "  INSTALL = 0\n" +
            "  LOGIN = 1\n" +
            "  SIGN_UP = 2\n" +
            "  REGISTER = 3\n" +
            "  PURCHASE = 4\n" +
            "  ADD_TO_CART = 5\n" +
            "  ADD_TO_WISHLIST = 6\n" +
            "  INITIATE_CHECKOUT = 7\n" +
            "  START_TRIAL = 8\n" +
            "  SUBSCRIBE = 9\n" +
            "  LEVEL_START = 10\n" +
            "  LEVEL_COMPLETE = 11\n" +
            "  TUTORIAL_COMPLETE = 12\n" +
            "  SEARCH = 13\n" +
            "  VIEW_ITEM = 14\n" +
            "  VIEW_CONTENT = 15\n" +
            "  SHARE = 16\n" +
            "  CUSTOM = 17";

        [Test]
        public void PublicApiMatchesSnapshot()
        {
            Assert.That(CreateSnapshot(), Is.EqualTo(ExpectedSnapshot));
        }

        private static string CreateSnapshot()
        {
            var assembly = typeof(AppstackSDK).Assembly;
            var publicTypes = assembly
                .GetTypes()
                .Where(type => type.IsPublic || type.IsNestedPublic)
                .OrderBy(type => type.FullName)
                .ToArray();
            var builder = new StringBuilder();

            foreach (var type in publicTypes)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                if (type.IsEnum)
                {
                    AppendEnum(builder, type);
                }
                else
                {
                    AppendClass(builder, type);
                }
            }

            return builder.ToString();
        }

        private static void AppendClass(StringBuilder builder, Type type)
        {
            builder.Append(type.IsAbstract && type.IsSealed
                ? "public static class "
                : "public class ");
            builder.Append(type.FullName);

            foreach (var method in type
                         .GetMethods(BindingFlags.Public | BindingFlags.Static |
                                     BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .OrderBy(method => method.MetadataToken))
            {
                builder.Append("\n  ");
                builder.Append(FormatType(method.ReturnType));
                builder.Append(' ');
                builder.Append(method.Name);
                builder.Append('(');
                builder.Append(string.Join(
                    ", ",
                    method.GetParameters().Select(FormatParameter)));
                builder.Append(')');
            }
        }

        private static void AppendEnum(StringBuilder builder, Type type)
        {
            builder.Append("public enum ");
            builder.Append(type.FullName);

            foreach (var name in Enum.GetNames(type))
            {
                builder.Append("\n  ");
                builder.Append(name);
                builder.Append(" = ");
                builder.Append(Convert.ToInt64(Enum.Parse(type, name)));
            }
        }

        private static string FormatParameter(ParameterInfo parameter)
        {
            var result = FormatType(parameter.ParameterType) + " " + parameter.Name;
            if (!parameter.HasDefaultValue)
            {
                return result;
            }

            return result + " = " +
                   (parameter.DefaultValue == null
                       ? "null"
                       : Convert.ToString(parameter.DefaultValue));
        }

        private static string FormatType(Type type)
        {
            if (type == typeof(void)) return "void";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(int)) return "int";
            if (type == typeof(string)) return "string";
            if (type == typeof(object)) return "object";

            if (!type.IsGenericType)
            {
                return type.Name;
            }

            var name = type.Name.Substring(0, type.Name.IndexOf('`'));
            return name + "<" +
                   string.Join(", ", type.GetGenericArguments().Select(FormatType)) +
                   ">";
        }
    }
}
