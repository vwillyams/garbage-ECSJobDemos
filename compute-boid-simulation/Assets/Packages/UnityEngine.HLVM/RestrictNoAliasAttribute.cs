using System;

namespace UnityEngine.Jobs
{
    /// <summary>
    /// Indicates that a symbol is not aliased in the current scope.
    /// It says that for the lifetime of the pointer,
    /// only the pointer itself or a value directly derived from it(such as pointer + 1)
    /// will be used to access the object to which it points.This limits the effects of pointer aliasing, aiding optimizations.
    /// If the declaration of intent is not followed and the object is accessed by an independent pointer, this will result in undefined behavior.   
    ///
    /// * RestrictNoAlias can be placed on a struct (All fields in the struct it will be marked non-aliased)
    /// * RestrictNoAlias can be placed on an interface (All fields of the struct implementing it will be marked non-aliased)
    /// * RestrictNoAlias can be placed on an parameter (The whole struct / builtin value will be marked non-aliased)
    /// </summary>
    public class RestrictNoAliasAttribute : System.Attribute
    {

    }
}
