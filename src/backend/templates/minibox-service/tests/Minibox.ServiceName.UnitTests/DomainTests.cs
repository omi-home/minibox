namespace Minibox.ServiceName.UnitTests;

/// <summary>Test đơn vị cho Domain/Application (aggregate, máy trạng thái, handler với fake). Thay test mẫu bằng test thật.</summary>
public class DomainTests
{
    [Fact(DisplayName = "Project UnitTests tham chiếu được Domain và Application")]
    public void layers_are_referenced()
    {
        Assert.Equal("Minibox.ServiceName.Domain", typeof(Domain.AssemblyReference).Assembly.GetName().Name);
        Assert.Equal("Minibox.ServiceName.Application", typeof(Application.AssemblyReference).Assembly.GetName().Name);
    }
}
