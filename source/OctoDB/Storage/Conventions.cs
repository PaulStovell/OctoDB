using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using OctoDB.Util;

namespace OctoDB.Storage
{
    public class Conventions
    {
        static readonly Regex pathFormatRegex = new Regex("^(?<prefix>[a-z]+\\\\)(\\{id\\})(?<suffix>((\\\\|\\.)[a-z]+)+)$", RegexOptions.Compiled);
        static readonly Dictionary<Type, string> pathFormatStrings = new Dictionary<Type, string>();
        static readonly Dictionary<Type, IPropertyReaderWriter<object>> idPropertyReaderWriters = new Dictionary<Type, IPropertyReaderWriter<object>>();
        static readonly Dictionary<Type, Regex> pathRecognizers = new Dictionary<Type, Regex>();
        static readonly ReaderWriterLockSlim sync = new ReaderWriterLockSlim();

        public static string GetPath(Type type, string id)
        {
            Ensure(type);

            sync.EnterReadLock();
            try
            {
                if (!pathFormatStrings.ContainsKey(type))
                {
                    return null;
                }
                return string.Format(pathFormatStrings[type], id);
            }
            finally
            {
                sync.ExitReadLock();
            }
        }

        public static bool IsSupported(Type type)
        {
            var attribute = (DocumentAttribute)type.GetCustomAttributes(typeof(DocumentAttribute), true).FirstOrDefault();
            if (attribute == null)
                return false;

            Ensure(type);
            return true;
        }

        public static string GetParentPath(Type type)
        {
            Ensure(type);

            sync.EnterReadLock();
            try
            {
                if (!pathFormatStrings.ContainsKey(type))
                {
                    return null;
                }

                var format = pathFormatStrings[type];
                return format.Substring(0, format.IndexOf("{0}") - 1);
            }
            finally
            {
                sync.ExitReadLock();
            }
        }

        public static string GetPath(object instance)
        {
            return GetPath(instance.GetType(), instance);
        }

        public static string GetPath(Type type, object instance)
        {
            Ensure(type);

            sync.EnterReadLock();
            try
            {
                var id = idPropertyReaderWriters[type].Read(instance);
                return string.Format(pathFormatStrings[type], id);
            }
            finally
            {
                sync.ExitReadLock();
            }
        }

        public static Type GetType(string path)
        {
            sync.EnterReadLock();
            try
            {
                foreach (var recognizer in pathRecognizers)
                {
                    var match = recognizer.Value.Match(path);
                    if (match.Success)
                    {
                        return recognizer.Key;
                    }
                }

                return null;
            }
            finally
            {
                sync.ExitReadLock();
            }
        }

        public static object GetId(object instance)
        {
            var type = instance.GetType();
            Ensure(type);
            return (object)idPropertyReaderWriters[type].Read(instance);
        }

        public static void AssignId(object instance, object id)
        {
            idPropertyReaderWriters[instance.GetType()].Write(instance, id);
        }

        public static void AssignId(string path, object instance)
        {
            var match = pathRecognizers[instance.GetType()].Match(path);
            if (match.Success)
            {
                var id = match.Groups["id"].Value;

                idPropertyReaderWriters[instance.GetType()].Write(instance, id);
            }
        }

        static void Ensure(Type type)
        {
            bool found = false;
            sync.EnterReadLock();
            try
            {
                found = pathFormatStrings.ContainsKey(type);
            }
            finally
            {
                sync.ExitReadLock();
            }

            if (!found)
            {
                Register(type);
            }
        }

        public static void Register(Assembly assembly)
        {
            sync.EnterWriteLock();
            try
            {
                var types =
                    from type in assembly.GetTypes()
                    where type.IsClass
                    let attribute = (DocumentAttribute)type.GetCustomAttributes(typeof(DocumentAttribute), true).FirstOrDefault()
                    where attribute != null
                    select type;

                foreach (var type in types)
                {
                    RegisterInternal(type);
                }
            }
            finally
            {
                sync.ExitWriteLock();
            }
        }

        public static void Register(Type type)
        {
            sync.EnterWriteLock();
            try
            {
                RegisterInternal(type);
            }
            finally
            {
                sync.ExitWriteLock();
            }
        }

        static void RegisterInternal(Type type)
        {
            var attribute = (DocumentAttribute)type.GetCustomAttributes(typeof(DocumentAttribute), true).FirstOrDefault();
            if (attribute == null)
                throw new InvalidOperationException(string.Format("The type '{0}' is not tagged with the [Document] attribute", type.FullName));
         
            if (pathFormatStrings.ContainsKey(type))
                return;

            var path = attribute.Path;
            var match = pathFormatRegex.Match(path);
            if (!match.Success)
                throw new FormatException(string.Format("Type '{0}' specifies an invalid path format: {1}", type.FullName, path));

            pathFormatStrings.Add(type, path.Replace("{id}", "{0}"));
            pathRecognizers.Add(type, new Regex(EscapeForRegex(match.Groups["prefix"].Value) + "(?<id>[A-Za-z0-9-]+)" + EscapeForRegex(match.Groups["suffix"].Value)));
            idPropertyReaderWriters.Add(type, PropertyReaderFactory.Create<object>(type, "Id"));
        }

        static string EscapeForRegex(string x)
        {
            return x.Replace("\\", "\\\\").Replace(".", "\\.");
        }
    }
}