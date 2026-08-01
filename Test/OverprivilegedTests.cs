using NUnit.Framework;
using SkillScanner.Models;
using SkillScanner.SkillRule;

namespace SkillScanner.Tests;

[TestFixture]
public class OverprivilegedTests
{
    private readonly Overprivileged _rule = new();

    [Test]
    public void Evaluate_WhenNetworkAccessIsRequired_ReturnsHighSeverityFinding()
    {
        var skill = new SkillData { Compatibility = "Requires network access." };

        var findings = _rule.Evaluate(skill).ToList();

        var finding = findings.Single();
        Assert.That(finding.Severity, Is.EqualTo(RuleSeverity.High));
        Assert.That(finding.Message, Does.Contain("Requires network access"));
    }

    [TestCase("Write the agent state to memory.md.")]
    [TestCase("Write the agent state to SOUL.MD.")]
    public void Evaluate_WhenIdentityFileIsReferenced_ReturnsHighSeverityFinding(string markdown)
    {
        var skill = new SkillData { SkillMarkDown = markdown };

        var findings = _rule.Evaluate(skill).ToList();

        var finding = findings.Single();
        Assert.That(finding.Severity, Is.EqualTo(RuleSeverity.High));
        Assert.That(finding.Message, Is.EqualTo("The skill has the write permission to identity files."));
    }

    [Test]
    public void Evaluate_WhenNoRiskyPermissionOrIdentityFileExists_ReturnsNoFindings()
    {
        var skill = new SkillData
        {
            Compatibility = "No external services required.",
            SkillMarkDown = "# Safe skill\nRead only local documentation."
        };

        var findings = _rule.Evaluate(skill);

        Assert.That(findings, Is.Empty);
    }
}
