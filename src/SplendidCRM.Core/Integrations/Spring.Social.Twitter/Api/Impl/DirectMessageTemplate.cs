#nullable disable
// .NET 10 Migration: DirectMessageTemplate stub updated to implement the migrated IDirectMessageOperations
// interface. All methods are stub implementations returning null/empty values — this is a dormant
// Enterprise Edition integration stub that compiles on .NET 10 but is not expected to execute.
// Method signatures updated to match IDirectMessageOperations: IList<T> return types, correct
// parameter signatures (page/pageSize instead of count), and Task-based async methods added.
// Callback-based async methods using Spring.Rest.Client.RestOperationCanceler removed — Spring.Rest.Client
// has no .NET Core / .NET 10 equivalent. Per AAP §0.8.1 minimal change clause.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    class DirectMessageTemplate : AbstractTwitterOperations, IDirectMessageOperations
    {
        // =====================================================================
        // IDirectMessageOperations — Synchronous implementations (stub)
        // =====================================================================

        public IList<DirectMessage> GetDirectMessagesReceived()
        {
            return new List<DirectMessage>();
        }

        public IList<DirectMessage> GetDirectMessagesReceived(int page, int pageSize)
        {
            return new List<DirectMessage>();
        }

        public IList<DirectMessage> GetDirectMessagesReceived(int page, int pageSize, long sinceId, long maxId)
        {
            return new List<DirectMessage>();
        }

        public IList<DirectMessage> GetDirectMessagesSent()
        {
            return new List<DirectMessage>();
        }

        public IList<DirectMessage> GetDirectMessagesSent(int page, int pageSize)
        {
            return new List<DirectMessage>();
        }

        public IList<DirectMessage> GetDirectMessagesSent(int page, int pageSize, long sinceId, long maxId)
        {
            return new List<DirectMessage>();
        }

        public DirectMessage GetDirectMessage(long id)
        {
            return null;
        }

        public DirectMessage SendDirectMessage(string toScreenName, string text)
        {
            return null;
        }

        public DirectMessage SendDirectMessage(long toUserId, string text)
        {
            return null;
        }

        public DirectMessage DeleteDirectMessage(long messageId)
        {
            return null;
        }

        // =====================================================================
        // IDirectMessageOperations — Task-based async stubs (.NET 10)
        // =====================================================================

        public Task<IList<DirectMessage>> GetDirectMessagesReceivedAsync()
        {
            return Task.FromResult<IList<DirectMessage>>(new List<DirectMessage>());
        }

        public Task<IList<DirectMessage>> GetDirectMessagesReceivedAsync(int page, int pageSize)
        {
            return Task.FromResult<IList<DirectMessage>>(new List<DirectMessage>());
        }

        public Task<IList<DirectMessage>> GetDirectMessagesReceivedAsync(int page, int pageSize, long sinceId, long maxId)
        {
            return Task.FromResult<IList<DirectMessage>>(new List<DirectMessage>());
        }

        public Task<IList<DirectMessage>> GetDirectMessagesSentAsync()
        {
            return Task.FromResult<IList<DirectMessage>>(new List<DirectMessage>());
        }

        public Task<IList<DirectMessage>> GetDirectMessagesSentAsync(int page, int pageSize)
        {
            return Task.FromResult<IList<DirectMessage>>(new List<DirectMessage>());
        }

        public Task<IList<DirectMessage>> GetDirectMessagesSentAsync(int page, int pageSize, long sinceId, long maxId)
        {
            return Task.FromResult<IList<DirectMessage>>(new List<DirectMessage>());
        }

        public Task<DirectMessage> GetDirectMessageAsync(long id)
        {
            return Task.FromResult<DirectMessage>(null);
        }

        public Task<DirectMessage> SendDirectMessageAsync(string toScreenName, string text)
        {
            return Task.FromResult<DirectMessage>(null);
        }

        public Task<DirectMessage> SendDirectMessageAsync(long toUserId, string text)
        {
            return Task.FromResult<DirectMessage>(null);
        }

        public Task<DirectMessage> DeleteDirectMessageAsync(long messageId)
        {
            return Task.FromResult<DirectMessage>(null);
        }
    }
}
