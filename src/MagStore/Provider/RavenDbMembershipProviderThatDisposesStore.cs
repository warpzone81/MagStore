﻿using System;

namespace MagStore.Provider
{
    public class RavenDbMembershipProviderThatDisposesStore : RavenDbMembershipProvider, IDisposable
    {
        public void Dispose()
        {
            if (DocumentStore != null)
                DocumentStore.Dispose();

            DocumentStore = null;
        }
    }
}