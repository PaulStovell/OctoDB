namespace OctoDB.Util
{
    interface IPropertyReaderWriter<TCast>
    {
        TCast Read(object target);
        void Write(object target, TCast value);
    }
}