using System;
using System.Collections.Generic;
using System.Reflection;

namespace OctoDB.Util
{
    static class PropertyReaderFactory
    {
        static readonly Dictionary<string, object> readers = new Dictionary<string, object>();

        public static IPropertyReaderWriter<TCast> Create<TCast>(Type objectType, string propertyName)
        {
            var key = objectType.AssemblyQualifiedName + "-" + propertyName;
            IPropertyReaderWriter<TCast> result = null;
            if (readers.ContainsKey(key))
            {
                result = readers[key] as IPropertyReaderWriter<TCast>;
            }

            if (result != null) 
                return result;

            var propertyInfo = objectType.GetProperty(propertyName);
            if (propertyInfo != null)
            {
                if (!typeof (TCast).IsAssignableFrom(propertyInfo.PropertyType))
                {
                    throw new InvalidOperationException(string.Format("Property type '{0}' for property '{1}.{2}' cannot be converted to type '{3}", propertyInfo.PropertyType, propertyInfo.DeclaringType == null ? "??" : propertyInfo.DeclaringType.Name, propertyInfo.Name, typeof(TCast).Name));
                }

                var delegateReaderType = typeof(Func<,>).MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType);
                var delegateWriterType = typeof(Action<,>).MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType);
                var readerType = typeof(DelegatePropertyReaderWriter<,,>).MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType, typeof(TCast));
                var propertyGetterMethodInfo = propertyInfo.GetGetMethod();
                if (propertyGetterMethodInfo == null)
                {
                    throw new ArgumentException(string.Format("The property '{0}' on type '{1}' does not contain a getter which could be accessed by the OctoDB binding infrastructure.", propertyName, propertyInfo.DeclaringType));
                }

                var propertyGetterDelegate = Delegate.CreateDelegate(delegateReaderType, propertyGetterMethodInfo);

                var propertySetterMethodInfo = propertyInfo.GetSetMethod(true);
                Delegate propertySetterDelegate = null;
                if (propertySetterMethodInfo != null)
                {
                    propertySetterDelegate = Delegate.CreateDelegate(delegateWriterType, propertySetterMethodInfo);
                }

                result = (IPropertyReaderWriter<TCast>)Activator.CreateInstance(readerType, propertyGetterDelegate, propertySetterDelegate);
                readers[key] = result;
            }
            else
            {
                var fieldInfo = objectType.GetField(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (fieldInfo == null) return null;
                if (!typeof(TCast).IsAssignableFrom(fieldInfo.FieldType))
                {
                    throw new InvalidOperationException(string.Format("Field type '{0}' for field '{1}.{2}' cannot be converted to type '{3}", fieldInfo.FieldType, fieldInfo.DeclaringType == null ? "??" : fieldInfo.DeclaringType.Name, fieldInfo.Name, typeof(TCast).Name));
                }

                result = new FieldReaderWriter<TCast>(fieldInfo);
                readers[key] = result;
            }

            return result;
        }

        class DelegatePropertyReaderWriter<TInput, TReturn, TCast> : IPropertyReaderWriter<TCast>
            where TReturn : TCast
        {
            readonly Func<TInput, TReturn> caller;
            readonly Action<TInput, TReturn> writer;

            public DelegatePropertyReaderWriter(Func<TInput, TReturn> caller, Action<TInput, TReturn> writer)
            {
                this.caller = caller;
                this.writer = writer;
            }

            public TCast Read(object target)
            {
                return caller((TInput)target);
            }

            public void Write(object target, TCast value)
            {
                writer((TInput)target, (TReturn)value);
            }
        }

        class FieldReaderWriter<TCast> : IPropertyReaderWriter<TCast>
        {
            readonly FieldInfo field;

            public FieldReaderWriter(FieldInfo field)
            {
                this.field = field;
            }

            public TCast Read(object target)
            {
                return (TCast)field.GetValue(target);
            }

            public void Write(object target, TCast value)
            {
                field.SetValue(target, value);
            }
        }
    }
}