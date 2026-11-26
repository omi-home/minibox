namespace Minibox.BuildingBlocks.Domain;

/// <summary>Gốc của value object: không định danh, bất biến, bằng nhau theo giá trị (nhờ <c>record</c>).</summary>
/// <remarks>Value object kiểm tra bất biến trong constructor và chỉ có thuộc tính get-only.</remarks>
public abstract record ValueObject;
