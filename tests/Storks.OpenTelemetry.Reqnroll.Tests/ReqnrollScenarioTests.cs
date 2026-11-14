using NUnit.Framework;

namespace Storks.OpenTelemetry.Reqnroll.Tests;

public class ReqnrollScenarioTests
{
    [Test]
    public void Error_text_of_a_thrown_error_is_its_message_then_its_stack_trace()
    {
        Exception thrown;
        try
        {
            throw new InvalidOperationException("Expected: 50m But was: 0m");
        }
        catch (InvalidOperationException ex)
        {
            thrown = ex;
        }

        var text = ReqnrollScenario.ErrorText(thrown);

        Assert.That(text, Does.StartWith("Expected: 50m But was: 0m\n\n   at "));
        Assert.That(text, Does.Contain(nameof(Error_text_of_a_thrown_error_is_its_message_then_its_stack_trace)));
    }

    [Test]
    public void Error_text_of_an_error_without_a_stack_trace_is_its_message()
    {
        Assert.That(ReqnrollScenario.ErrorText(new InvalidOperationException("Step is pending")), Is.EqualTo("Step is pending"));
    }

    [Test]
    public void Error_text_without_an_error_is_null()
    {
        Assert.That(ReqnrollScenario.ErrorText(null), Is.Null);
    }
}
