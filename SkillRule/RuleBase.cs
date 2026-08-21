
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.Net.Mail;
using System.Reflection.Metadata;
using SkillScanner.LLMClient;
using SkillScanner.Models;
using SkillScanner.SkillRule;

public abstract class RuleBase : IRule
{
    protected abstract RuleType RuleType { get; }

    protected abstract string SystemPrompt { get; }

    protected ILLMClient LLMClient { get; set; }

    protected IMarkdownChunker MarkdownChunker {get;set;}
    protected IVectorRepository VectorRepository {get;set;}
    protected IEmbeddingClient EmbeddingClient{get;set;}

    public RuleBase(ILLMClient llmClient, IVectorRepository vectorRepository, IEmbeddingClient embeddingClient, IMarkdownChunker markdownChunker)
    {
        LLMClient = llmClient;
        VectorRepository=vectorRepository;
        EmbeddingClient=embeddingClient;
        MarkdownChunker=markdownChunker;
    }
    protected static int SuccessCount = 0;
    protected static int FailureCount = 0;
    
    protected static int TotalCount=0;

    
    protected const string JsonContract = """
        Respond with ONLY a JSON object, no other text, no markdown fences, in this exact shape:
        {"isFlagged": bool, "confidence": number between 0 and 1, "reasoning": "one sentence"}
        """;

    protected abstract IEnumerable<RuleResult> EvaluateDeterministic(SkillData skillData);

    protected async Task<RuleResult> EvaluateWithLLMAsync(SkillData skillData, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        if (skillData == null || string.IsNullOrWhiteSpace(SystemPrompt))
        {
            throw new InvalidOperationException("Invalid skill data or system prompt for LLM evaluation.");
        }
//Make this thread safe by using Interlocked.Increment to increment the TotalCount variable atomically  
       Interlocked.Increment(ref TotalCount);

        LLMVerdict llmVerdict;
        try
        {
            llmVerdict = await LLMClient.GetResponseAsync(
                SystemPrompt,
                skillData.SkillMarkdownContent ?? "",
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            Interlocked.Increment(ref FailureCount);
            Console.WriteLine($"LLM evaluation timed out after {stopwatch.Elapsed} for rule: {RuleType.Name}");
            return new RuleResult();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Interlocked.Increment(ref FailureCount);
            Console.WriteLine($"LLM evaluation failed after {stopwatch.Elapsed} for rule: {RuleType.Name}. {ex.Message}");
            return new RuleResult();
        }

        stopwatch.Stop();
        Console.WriteLine($"LLM evaluation completed in {stopwatch.Elapsed} seconds for rule: {RuleType.Name}");
        if (llmVerdict == null)     
        {
            Interlocked.Increment(ref FailureCount);
            Console.WriteLine($"LLM evaluation returned no verdict for rule: {RuleType.Name}");
            return new RuleResult();
        }
        if (llmVerdict.IsFlagged)
        {
             Interlocked.Increment(ref SuccessCount);
           
            return new RuleResult
            {
                RuleType = this.RuleType,
                Message = llmVerdict.Reasoning ?? "The skill is non-compliant based on LLM evaluation.",
                IsFlagged = true
            };
        }
       
        return new RuleResult();
    }

   
    protected  async Task<IEnumerable<ChunkJudgment>> EvaluateWithVectorSimilarityAndLLM(SkillData skillData, CancellationToken cancellationToken=default)
    {
       List<ChunkJudgment> chunkJudgments=new List<ChunkJudgment>();
        if(skillData.SkillMarkdown==null)
        {
            throw new ArgumentNullException();
        }
        var documentChunks= MarkdownChunker.Chunk(skillData.SkillMarkdown);
        LLMVerdict lLMVerdict= new LLMVerdict();
        foreach(var chunk in documentChunks)
        {
            
            var embeddedChunks= await EmbeddingClient.GetEmbeddingAsync(chunk.Content);
            var similarChunks= await VectorRepository.SearchSimilarTextAsync(embeddedChunks, RuleType.Id);
            foreach(var item in similarChunks)
            {
                Console.WriteLine($"similarChunk: {item.Content} {item.Distance}, {item.RuleId}, {item.TextId}");
            }
            if(similarChunks.Count()==0)
              continue;

            var allBenign= similarChunks.All(chunk=> chunk.RuleId==null && chunk.Distance<=0.5);
            var allViolation= similarChunks.All(chunk=> chunk.RuleId==RuleType.Id && chunk.Distance<=0.5);
            if(allBenign || allViolation)
            {
               lLMVerdict=new LLMVerdict()
               {
                IsFlagged = allViolation,
                 Confidence = 1.0 - similarChunks.First().Distance,
                 Reasoning = $"Unanimous match against {(allViolation ? "violation" : "benign")} reference examples.",
                 Source = VerdictSource.RagGrounded
               };
            }
            else
            {
                var benignChunk=similarChunks.FirstOrDefault(chunk=>chunk.RuleId==null);
                var violatedChunk= similarChunks.FirstOrDefault(chunk=>chunk.RuleId==RuleType.Id);
                var ambiguousPrompt= PromptBuilder.BuildAmbiguousPrompt(chunk.Content, violatedChunk, benignChunk);
                lLMVerdict=await LLMClient.GetResponseAsync( SystemPrompt,ambiguousPrompt,  cancellationToken);
                lLMVerdict.Source=VerdictSource.LLMGrounded;
                
            }
            Console.WriteLine($"LLMVerdict: {lLMVerdict.Reasoning}");
           chunkJudgments.Add(new ChunkJudgment()
           {
               Chunk=chunk,
               Matches=new List<TextCorpusMatch>(similarChunks),
               Verdict=lLMVerdict,
           });

        }
        return  chunkJudgments;
    }
    public virtual async Task<IEnumerable<RuleResult>> EvaluateAsync(SkillData skillData, CancellationToken cancellationToken = default)
    {
       
        var ruleResults = new List<RuleResult>();
        ruleResults = EvaluateDeterministic(skillData).ToList();


        if (ruleResults.Count.Equals(0))
        {
            // If no deterministic results, use LLM for evaluation
            var fallbackResult = await EvaluateWithLLMAsync(skillData, cancellationToken);
            if (fallbackResult != null && !string.IsNullOrEmpty(fallbackResult.Message))
            {
                ruleResults.Add(fallbackResult);
            }
        }

        
        return ruleResults;
    }

    public virtual async Task<IEnumerable<RuleResult>> EvaluateAsyncWithRAG(SkillData skillData, CancellationToken cancellationToken=default)
    {
        var ruleResults = EvaluateDeterministic(skillData).ToList();

    if (ruleResults.Count == 0)
    {
        var chunkJudgments = await EvaluateWithVectorSimilarityAndLLM(skillData, cancellationToken);
        Console.WriteLine("Jugements Count", chunkJudgments.Count());
        var violatedChunks = chunkJudgments.Where(c => c.Verdict.IsFlagged).ToList();

        if (violatedChunks.Count > 0)
        {
            ruleResults.Add(new RuleResult
            {
                IsFlagged = true,
                Message = "Check following content: " + string.Join(" | ", violatedChunks.Select(c => c.Chunk.Content)),
                RuleType = RuleType
            });
        }
    }

    return ruleResults;
    }
   
    public void CountCalls()
    {
        Console.WriteLine($"Total Success Count: {SuccessCount}, Total Failure Count: {FailureCount}, UnFlagged Count: {TotalCount - SuccessCount - FailureCount}");
        Console.WriteLine($"Total Evaluations: {TotalCount}");
    }

   
}
