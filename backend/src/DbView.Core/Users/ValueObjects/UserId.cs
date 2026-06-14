using DbView.Core.Abstractions;
using DbView.Core.Exceptions;

namespace DbView.Core
{
     public sealed class UserId : ValueObject, IEquatable<UserId>
     {
         public long Value { get; }

         private UserId(long value)
         {
             Value = value;
         }
         public static UserId New() =>new(0);
         public static UserId From(long value)
         {
             

             return new UserId(value);
         }

         // 实现 IEquatable<UserId>
         public bool Equals(UserId? other)
         {
             if (other is null) return false;
             if (ReferenceEquals(this, other)) return true;
             return Value.Equals(other.Value);
         }

         // 重写 Equals
         public override bool Equals(object? obj)
         {
             return Equals(obj as UserId);
         }

         // 重写 GetHashCode
         public override int GetHashCode()
         {
             return Value.GetHashCode();
         }

         // 操作符重载
         public static bool operator ==(UserId? left, UserId? right)
         {
             if (left is null && right is null) return true;
             if (left is null || right is null) return false;
             return left.Equals(right);
         }

         public static bool operator !=(UserId? left, UserId? right)
         {
             return !(left == right);
         }

         // ValueObject 要求的相等性组件
         protected override IEnumerable<object> GetEqualityComponents()
         {
             yield return Value;
         }

         public override string ToString() => Value.ToString();
     }
}



