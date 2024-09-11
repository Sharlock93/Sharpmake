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
        public class PSVitaEnvironmentResolver : EnvironmentVariableResolver
        {

            private static string s_vita_sdk;
            [Resolvable]
            public static string SCE_PSP2_SDK_DIR
            {
                get
                {
                    return Util.GetEnvironmentVariable("SCE_PSP2_SDK_DIR", @"C:\PSVITA\sdk", ref s_vita_sdk);
                }
            }

            public PSVitaEnvironmentResolver()
            {

            }

            public PSVitaEnvironmentResolver(params VariableAssignment[] vars) : base(vars)
            {

            }
        }

    }

}
