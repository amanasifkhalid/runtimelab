// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Numerics.Hashing;
using Microsoft.CSharp.RuntimeBinder.Errors;
using Microsoft.CSharp.RuntimeBinder.Semantics;

namespace Microsoft.CSharp.RuntimeBinder
{
    /// <summary>
    /// Represents a dynamic property access in C#, providing the binding semantics and the details about the operation.
    /// Instances of this class are generated by the C# compiler.
    /// </summary>
    internal sealed class CSharpSetMemberBinder : SetMemberBinder, ICSharpBinder
    {
        public BindingFlag BindingFlags => 0;

        [RequiresUnreferencedCode(Binder.TrimmerWarning)]
        public Expr DispatchPayload(RuntimeBinder runtimeBinder, ArgumentObject[] arguments, LocalVariableSymbol[] locals)
            => runtimeBinder.BindAssignment(this, arguments, locals);

        [RequiresUnreferencedCode(Binder.TrimmerWarning)]
        public void PopulateSymbolTableWithName(Type callingType, ArgumentObject[] arguments)
            => SymbolTable.PopulateSymbolTableWithName(Name, null, arguments[0].Type);

        public bool IsBinderThatCanHaveRefReceiver => false;

        internal bool IsCompoundAssignment { get; }

        private readonly CSharpArgumentInfo[] _argumentInfo;

        CSharpArgumentInfo ICSharpBinder.GetArgumentInfo(int index) => _argumentInfo[index];

        private readonly RuntimeBinder _binder;

        private readonly Type _callingContext;

        private bool IsChecked => _binder.IsChecked;

        //////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Initializes a new instance of the <see cref="SetMemberBinder" />.
        /// </summary>
        /// <param name="name">The name of the member to get.</param>
        /// <param name="isCompoundAssignment">True if the assignment comes from a compound assignment in source.</param>
        /// <param name="isChecked">True if the operation is defined in a checked context; otherwise, false.</param>
        /// <param name="callingContext">The <see cref="Type"/> that indicates where this operation is defined.</param>
        /// <param name="argumentInfo">The sequence of <see cref="CSharpArgumentInfo"/> instances for the arguments to this operation.</param>
        [RequiresUnreferencedCode(Binder.TrimmerWarning)]
        public CSharpSetMemberBinder(
            string name,
            bool isCompoundAssignment,
            bool isChecked,
            Type callingContext,
            IEnumerable<CSharpArgumentInfo> argumentInfo) :
            base(name, false)
        {
            IsCompoundAssignment = isCompoundAssignment;
            _argumentInfo = BinderHelper.ToArray(argumentInfo);
            _callingContext = callingContext;
            _binder = new RuntimeBinder(callingContext, isChecked);
        }

        public int GetGetBinderEquivalenceHash()
        {
            int hash = _callingContext?.GetHashCode() ?? 0;
            if (IsChecked)
            {
                hash = HashHelpers.Combine(hash, 1);
            }
            if (IsCompoundAssignment)
            {
                hash = HashHelpers.Combine(hash, 1);
            }
            hash = HashHelpers.Combine(hash, Name.GetHashCode());
            hash = BinderHelper.AddArgHashes(hash, _argumentInfo);

            return hash;
        }

        public bool IsEquivalentTo(ICSharpBinder other)
        {
            var otherBinder = other as CSharpSetMemberBinder;
            if (otherBinder == null)
            {
                return false;
            }

            if (Name != otherBinder.Name ||
                _callingContext != otherBinder._callingContext ||
                IsChecked != otherBinder.IsChecked ||
                IsCompoundAssignment != otherBinder.IsCompoundAssignment ||
                _argumentInfo.Length != otherBinder._argumentInfo.Length)
            {
                return false;
            }

            return BinderHelper.CompareArgInfos(_argumentInfo, otherBinder._argumentInfo);
        }

        /// <summary>
        /// Performs the binding of the dynamic set member operation if the target dynamic object cannot bind.
        /// </summary>
        /// <param name="target">The target of the dynamic set member operation.</param>
        /// <param name="value">The value to set to the member.</param>
        /// <param name="errorSuggestion">The binding result to use if binding fails, or null.</param>
        /// <returns>The <see cref="DynamicMetaObject"/> representing the result of the binding.</returns>
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
            Justification = "This whole class is unsafe. Constructors are marked as such.")]
        public override DynamicMetaObject FallbackSetMember(DynamicMetaObject target, DynamicMetaObject value, DynamicMetaObject errorSuggestion)
        {
#if ENABLECOMBINDER
            DynamicMetaObject com;
            if (ComInterop.ComBinder.TryBindSetMember(this, target, value, out com))
            {
                return com;
            }
#else
            BinderHelper.ThrowIfUsingDynamicCom(target);
#endif

            BinderHelper.ValidateBindArgument(target, nameof(target));
            BinderHelper.ValidateBindArgument(value, nameof(value));
            return BinderHelper.Bind(this, _binder, new[] { target, value }, _argumentInfo, errorSuggestion);
        }
    }
}
