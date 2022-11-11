// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

namespace Microsoft.CodeAnalysis.Sarif.Sarifer
{
    internal class CancellableProxy : MarshalByRefObject, ITokenSource, IDisposable
    {
        private readonly CancellationTokenSource tokenSource;

        public CancellableProxy()
        {
            this.tokenSource = new CancellationTokenSource();
        }

        public CancellationToken Token => this.tokenSource.Token;

        public void Cancel()
        {
            this.tokenSource.Cancel();
        }

        public void Dispose()
        {
            this.tokenSource.Dispose();
        }
    }
}
