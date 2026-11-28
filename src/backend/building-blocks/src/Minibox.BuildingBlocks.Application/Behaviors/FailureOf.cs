using System.Reflection;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Application.Behaviors;

/// <summary>Tạo <typeparamref name="TResponse"/> thất bại từ <see cref="Error"/> (Result hoặc Result&lt;T&gt;).</summary>
static class FailureOf<TResponse> where TResponse : Result
{
    static readonly Func<Error, TResponse> Create = typeof(TResponse) == typeof(Result)
        ? e => (TResponse)Result.Failure(e)
        : typeof(TResponse).GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, [typeof(Error)])!
            .CreateDelegate<Func<Error, TResponse>>();

    public static TResponse From(Error error) => Create(error);
}
