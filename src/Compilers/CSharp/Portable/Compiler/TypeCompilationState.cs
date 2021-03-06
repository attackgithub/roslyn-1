﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    /// <summary>
    /// Represents the state of compilation of one particular type.
    /// This includes, for example, a collection of synthesized methods created during lowering. 
    /// </summary>
    /// <remarks>
    /// WARNING: Note that the collection class is not thread-safe and will 
    /// need to be revised if emit phase is changed to support multithreading when
    /// translating a particular type.
    /// </remarks>
    internal sealed class TypeCompilationState
    {
        /// <summary> Synthesized method info </summary>
        internal struct MethodWithBody
        {
            public readonly MethodSymbol Method;
            public readonly BoundStatement Body;
            public readonly ImportChain ImportChainOpt;

            internal MethodWithBody(MethodSymbol method, BoundStatement body, ImportChain importChainOpt)
            {
                Debug.Assert(method != null);
                Debug.Assert(body != null);

                this.Method = method;
                this.Body = body;
                this.ImportChainOpt = importChainOpt;
            }
        }

        /// <summary> Flat array of created methods, non-empty if not-null </summary>
        private ArrayBuilder<MethodWithBody> _synthesizedMethods;

        /// <summary> 
        /// Map of wrapper methods created for base access of base type virtual methods from 
        /// other classes (like those created for lambdas...); actually each method symbol will 
        /// only need one wrapper to call it non-virtually.
        /// </summary>
        private Dictionary<MethodSymbol, MethodSymbol> _wrappers;

        /// <summary>
        /// Type symbol being compiled, or null if we compile a synthesized type that doesn't have a symbol (e.g. PrivateImplementationDetails).
        /// </summary>
        private readonly NamedTypeSymbol _typeOpt;

        /// <summary>
        /// The builder for generating code, or null if not in emit phase.
        /// </summary>
        public readonly PEModuleBuilder ModuleBuilderOpt;

        /// <summary>
        /// Any generated methods that don't suppress debug info will use this
        /// list of debug imports.
        /// </summary>
        public ImportChain CurrentImportChain { get; set; }

        public readonly CSharpCompilation Compilation;

        public LambdaFrame staticLambdaFrame;

        public TypeCompilationState(NamedTypeSymbol typeOpt, CSharpCompilation compilation, PEModuleBuilder moduleBuilderOpt)
        {
            this.Compilation = compilation;
            _typeOpt = typeOpt;
            this.ModuleBuilderOpt = moduleBuilderOpt;
        }

        /// <summary>
        /// The type for which this compilation state is being used.
        /// </summary>
        public NamedTypeSymbol Type
        {
            get
            {
                // NOTE: currently it can be null if only private implementation type methods are compiled
                Debug.Assert((object)_typeOpt != null);
                return _typeOpt;
            }
        }

        public bool Emitting
        {
            get { return ModuleBuilderOpt != null; }
        }

        /// <summary> 
        /// Add a 'regular' synthesized method.
        /// </summary>
        public bool HasSynthesizedMethods
        {
            get { return _synthesizedMethods != null; }
        }

        public ArrayBuilder<MethodWithBody> SynthesizedMethods
        {
            get { return _synthesizedMethods; }
        }

        public void AddSynthesizedMethod(MethodSymbol method, BoundStatement body)
        {
            if (_synthesizedMethods == null)
            {
                _synthesizedMethods = ArrayBuilder<MethodWithBody>.GetInstance();
            }

            _synthesizedMethods.Add(new MethodWithBody(method, body, method.GenerateDebugInfo ? CurrentImportChain : null));
        }

        /// <summary> 
        /// Add a 'wrapper' synthesized method and map it to the original one so it can be reused. 
        /// </summary>
        /// <remarks>
        /// Wrapper methods are created for base access of base type virtual methods from 
        /// other classes (like those created for lambdas...).
        /// </remarks>
        public void AddMethodWrapper(MethodSymbol method, MethodSymbol wrapper, BoundStatement body)
        {
            this.AddSynthesizedMethod(wrapper, body);

            if (_wrappers == null)
            {
                _wrappers = new Dictionary<MethodSymbol, MethodSymbol>();
            }

            _wrappers.Add(method, wrapper);
        }

        /// <summary> The index of the next wrapped method to be used </summary>
        public int NextWrapperMethodIndex
        {
            get { return _wrappers == null ? 0 : _wrappers.Count; }
        }

        /// <summary> 
        /// Get a 'wrapper' method for the original one. 
        /// </summary>
        /// <remarks>
        /// Wrapper methods are created for base access of base type virtual methods from 
        /// other classes (like those created for lambdas...).
        /// </remarks>
        public MethodSymbol GetMethodWrapper(MethodSymbol method)
        {
            MethodSymbol wrapper = null;
            return _wrappers != null && _wrappers.TryGetValue(method, out wrapper) ? wrapper : null;
        }

        /// <summary> Free resources allocated for this method collection </summary>
        public void Free()
        {
            if (_synthesizedMethods != null)
            {
                _synthesizedMethods.Free();
                _synthesizedMethods = null;
            }

            _wrappers = null;
        }
    }
}
