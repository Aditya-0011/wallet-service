using System.Text.Json.Serialization;
using Wallet.Models;

namespace Wallet.Utils;

[JsonSerializable(typeof(List<TransactionUpdateHistoryRow>))]
internal partial class WalletJsonSerializerContext : JsonSerializerContext;
