using System;
using System.Collections.Generic;
using System.Reflection;

namespace Core.Events
{
    /// <summary>
    /// Discovers event types by scanning the loaded assemblies. Used at runtime, where Unity editor
    /// APIs are unavailable.
    /// </summary>
    /// <remarks>
    /// The scan is restricted to the assembly that declares <see cref="IEvent"/> and the assemblies
    /// that reference it, which is both cheaper than a full scan and complete: a type cannot
    /// implement the interface without referencing its assembly. This is the significant difference
    /// from a scan of the predefined Assembly-CSharp assemblies, which cannot see event types
    /// declared inside assembly definition files.
    /// </remarks>
    public sealed class AppDomainEventTypeProvider : IEventTypeProvider
    {
        public IReadOnlyList<Type> GetEventTypes()
        {
            var eventInterface = typeof(IEvent);
            var declaringAssemblyName = eventInterface.Assembly.GetName().Name;
            var results = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!CanContainEvents(assembly, declaringAssemblyName)) continue;

                foreach (var type in GetLoadableTypes(assembly))
                {
                    if (type == null) continue;
                    if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition) continue;
                    if (!eventInterface.IsAssignableFrom(type)) continue;

                    results.Add(type);
                }
            }

            return results;
        }

        /// <summary>
        /// Determines whether <paramref name="assembly"/> could declare an event type, that is,
        /// whether it is or references the assembly named <paramref name="declaringAssemblyName"/>.
        /// </summary>
        static bool CanContainEvents(Assembly assembly, string declaringAssemblyName)
        {
            if (assembly.IsDynamic) return false;
            if (assembly.GetName().Name == declaringAssemblyName) return true;

            foreach (var reference in assembly.GetReferencedAssemblies())
                if (reference.Name == declaringAssemblyName)
                    return true;

            return false;
        }

        /// <summary>
        /// Returns the types of <paramref name="assembly"/>, degrading to the subset that loaded
        /// successfully when a dependency is missing rather than failing the whole scan.
        /// </summary>
        static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types;
            }
        }
    }
}
