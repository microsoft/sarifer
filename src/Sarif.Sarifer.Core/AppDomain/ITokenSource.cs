﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Microsoft.CodeAnalysis.Sarif.Sarifer
{
    public interface ITokenSource
    {
        CancellationToken Token { get; }
    }
}
