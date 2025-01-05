using System.Threading.Tasks;

namespace Ez;

public interface IJobTrigger
{
    Task Subscribe(IJobContext context);
}

