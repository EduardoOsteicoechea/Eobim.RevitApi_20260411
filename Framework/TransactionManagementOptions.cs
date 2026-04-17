namespace Eobim.RevitApi.Framework;

public enum TransactionManagementOptions
{
    None = 0,
    TransactionlessAction = 1,
    RequiresDedicatedTransactionForAction = 2,
    RequiresEnclosingTransactionForCommand = 3,
}