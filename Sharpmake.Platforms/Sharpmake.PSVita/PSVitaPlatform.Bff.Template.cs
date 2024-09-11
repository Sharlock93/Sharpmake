// Copyright (c) Ubisoft. All Rights Reserved.
// Licensed under the Apache 2.0 License. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharpmake.PSVita
{
    public static partial class PSVita
    {
        public sealed partial class PSVitaPlatform
        {

            public const string _linkerOptionsTemplate = @"
    .LinkerOptions          = '-o ""%2"" ""%1""'
                            // System options
                            // -------------------------
                            // 
                            // Library Search Path
                            // ---------------------------
                            // Libraries
                            // ---------------------------
                            // Options
                            //--------
                            // Additional linker options
                            //--------------------------
";


            public const string _compilerExtraOptions = @"
    .CompilerExtraOptions   = ''
            // General options
            // -------------------------
            // Additional compiler options
            //--------------------------
";

            public const string _compilerOptimizationOptions =
                @"
    // Optimizations options
    // ---------------------
    .CompilerOptimizations = ''
";
        }
    }
}
