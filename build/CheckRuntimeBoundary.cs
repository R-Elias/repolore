using (var stream = File.OpenRead(AssemblyPath))
using (var pe = new PEReader(stream))
{
    var metadata = pe.GetMetadataReader();
    foreach (var handle in metadata.TypeReferences)
    {
        var type = metadata.GetTypeReference(handle);
        var ns = metadata.GetString(type.Namespace);
        var name = metadata.GetString(type.Name);
        var forbidden = ns == "System.Net" || ns.StartsWith("System.Net.", StringComparison.Ordinal)
            || (ns == "System.Diagnostics" && (name.StartsWith("Process", StringComparison.Ordinal) || name == "Activity" || name == "ActivitySource"))
            || ns == "System.Runtime.Loader"
            || ns == "System.Runtime.InteropServices" || ns.StartsWith("System.Runtime.InteropServices.", StringComparison.Ordinal)
            || (ns == "System.Reflection" && !name.EndsWith("Attribute", StringComparison.Ordinal))
            || ns.StartsWith("System.Reflection.", StringComparison.Ordinal)
            || (ns == "System" && (name == "Type" || name == "Activator" || name == "AppDomain" || name == "Delegate"))
            || (ns == "System.Runtime.CompilerServices" && (name == "Unsafe" || name.StartsWith("CallSite", StringComparison.Ordinal)))
            || ns.StartsWith("Microsoft.CSharp", StringComparison.Ordinal)
            || ns == "System.Linq.Expressions";
        if (forbidden)
            Log.LogError("RLBOUNDARY: forbidden runtime capability: " + ns + "." + name + " in " + AssemblyPath);
    }
    foreach (var handle in metadata.MethodDefinitions)
    {
        var method = metadata.GetMethodDefinition(handle);
        if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0
            || (method.ImplAttributes & (MethodImplAttributes.Native | MethodImplAttributes.Unmanaged | MethodImplAttributes.InternalCall)) != 0)
            Log.LogError("RLBOUNDARY: native/runtime interop method: " + metadata.GetString(method.Name));
    }
}
