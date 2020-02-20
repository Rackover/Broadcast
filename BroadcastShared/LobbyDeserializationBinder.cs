using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace Broadcast.Shared
{
    public sealed class LobbyDeserializationBinder : SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            // For each assemblyName/typeName that you want to deserialize to
            // a different type, set typeToDeserialize to the desired type.
            String exeAssembly = Assembly.GetExecutingAssembly().FullName;


            // The following line of code returns the type.
            var typeToDeserialize = Type.GetType(String.Format("{0}, {1}",
                typeName, exeAssembly));

            return typeToDeserialize;
        }
    }
}
