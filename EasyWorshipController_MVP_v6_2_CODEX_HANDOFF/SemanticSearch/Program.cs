using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

// Offline, stdin/stdout-only worker. No network access or presentation operations.
internal sealed class Bert : IDisposable
{
    readonly Dictionary<string,int> vocabulary;
    readonly InferenceSession session;
    readonly bool mean;
    public Bert(string model,string vocab,bool meanPool=false)
    {
        mean=meanPool;
        vocabulary=File.ReadLines(vocab).Select((w,i)=>(w,i)).ToDictionary(x=>x.w,x=>x.i);
        using var options=new SessionOptions { IntraOpNumThreads=2,InterOpNumThreads=1 };
        session=new InferenceSession(model,options);
    }
    public List<long> Tokens(string text)
    {
        string normalized=string.Concat(text.ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark));
        var result=new List<long>();
        foreach(Match match in Regex.Matches(normalized,@"[\p{L}\p{N}_]+|[^\s\p{L}\p{N}_]"))
        {
            string word=match.Value;var pieces=new List<long>();int start=0;
            if(word.Length>100){result.Add(100);continue;}
            while(start<word.Length)
            {
                int end=word.Length,id=-1;
                for(;end>start;end--)if(vocabulary.TryGetValue((start>0?"##":"")+word[start..end],out id))break;
                if(end==start){pieces.Clear();pieces.Add(100);break;}
                pieces.Add(id);start=end;
            }
            result.AddRange(pieces);
        }
        return result;
    }
    public float[][] Run(IReadOnlyList<string> first,IReadOnlyList<string>? second=null)
    {
        var rows=new List<(long[],long[])>();
        for(int i=0;i<first.Count;i++)
        {
            var a=Tokens(first[i]);var b=second==null?null:Tokens(second[i]);
            int budget=second==null?190:253;
            while(a.Count+(b?.Count??0)>budget){if(b!=null && b.Count>a.Count)b.RemoveAt(b.Count-1);else a.RemoveAt(a.Count-1);}
            var ids=new List<long>{101};ids.AddRange(a);ids.Add(102);var segments=Enumerable.Repeat(0L,ids.Count).ToList();
            if(b!=null){ids.AddRange(b);ids.Add(102);segments.AddRange(Enumerable.Repeat(1L,b.Count+1));}
            rows.Add((ids.ToArray(),segments.ToArray()));
        }
        int width=rows.Max(r=>r.Item1.Length),batch=rows.Count;
        var idsTensor=new DenseTensor<long>(new[]{batch,width});var mask=new DenseTensor<long>(new[]{batch,width});var types=new DenseTensor<long>(new[]{batch,width});
        for(int i=0;i<batch;i++)for(int j=0;j<rows[i].Item1.Length;j++){idsTensor[i,j]=rows[i].Item1[j];mask[i,j]=1;types[i,j]=rows[i].Item2[j];}
        var inputs=new List<NamedOnnxValue>{NamedOnnxValue.CreateFromTensor("input_ids",idsTensor),NamedOnnxValue.CreateFromTensor("attention_mask",mask)};
        if(session.InputMetadata.ContainsKey("token_type_ids"))inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids",types));
        using var outputs=session.Run(inputs);var tensor=outputs.First().AsTensor<float>();var vectors=new float[batch][];
        for(int i=0;i<batch;i++)
        {
            int dim=tensor.Dimensions[^1];vectors[i]=new float[dim];
            for(int d=0;d<dim;d++)
            {
                if(tensor.Rank==3 && mean){float sum=0;for(int t=0;t<rows[i].Item1.Length;t++)sum+=tensor[i,t,d];vectors[i][d]=sum/rows[i].Item1.Length;}
                else vectors[i][d]=tensor.Rank==3?tensor[i,0,d]:tensor[i,d];
            }
            if(second==null){double norm=Math.Sqrt(vectors[i].Sum(v=>(double)v*v));if(norm>0)for(int d=0;d<dim;d++)vectors[i][d]/=(float)norm;}
        }
        return vectors;
    }
    public void Dispose()=>session.Dispose();
}

internal static class Program
{
    static string Hash(string file)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
    static float[][] LoadVectors(string cache,string identity,string[] texts,Bert embed,bool build,string prefix="")
    {
        if(File.Exists(cache))
        {
            using var reader=new BinaryReader(File.OpenRead(cache));
            if(reader.ReadString()!=identity)throw new InvalidDataException("Search index differs from corpus/model: "+cache);
            int count=reader.ReadInt32(),dim=reader.ReadInt32();if(count!=texts.Length || dim!=384)throw new InvalidDataException("Invalid search index dimensions");
            var result=new float[count][];for(int i=0;i<count;i++){result[i]=new float[dim];for(int j=0;j<dim;j++)result[i][j]=reader.ReadSingle();}return result;
        }
        if(!build)throw new FileNotFoundException("Semantic index missing; run SETUP_SCRIPTURE_SEARCH.ps1 first.");
        var vectors=new float[texts.Length][];
        // Similar lengths share a batch to reduce padding work on ordinary CPUs.
        var order=Enumerable.Range(0,texts.Length).OrderBy(i=>texts[i].Length).ToArray();
        for(int i=0;i<texts.Length;i+=16)
        {
            var ids=order.Skip(i).Take(16).ToArray();var batch=embed.Run(ids.Select(id=>prefix+texts[id]).ToArray());
            for(int j=0;j<ids.Length;j++)vectors[ids[j]]=batch[j];
            if(i%1024==0)Console.Error.WriteLine($"Indexing {Path.GetFileName(cache)} {i}/{texts.Length}");
        }
        string temp=cache+".partial";
        using(var writer=new BinaryWriter(File.Create(temp))){writer.Write(identity);writer.Write(texts.Length);writer.Write(vectors[0].Length);foreach(var v in vectors)foreach(float f in v)writer.Write(f);}
        File.Move(temp,cache,true);return vectors;
    }
    static int Main(string[] args)
    {
        try{return Run(args);}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static int Run(string[] args)
    {
        if(args.Length<2)throw new ArgumentException("Usage: Gazely.Search <model directory> <corpus.tsv> [--index]");
        string models=Path.GetFullPath(args[0]),corpus=Path.GetFullPath(args[1]);
        bool e5=File.Exists(Path.Combine(models,"e5-mode"));
        using var embed=new Bert(Path.Combine(models,"embedding.onnx"),Path.Combine(models,"vocab.txt"),e5);
        string[] rows=File.ReadAllLines(corpus);string[] texts=rows.Select(r=>r.Split('\t',4)[3]).ToArray();
        string identity=Hash(corpus)+Hash(Path.Combine(models,"embedding.onnx"))+Hash(Path.Combine(models,"vocab.txt"))+(e5?"mean192-e5-v1":"cls192-v1");
        bool build=args.Contains("--index");
        float[][] vectors=LoadVectors(Path.Combine(models,"kjv-vectors.bin"),identity,texts,embed,build,e5?"passage: ":"");
        var canonical=Enumerable.Range(0,rows.Length).ToList();
        string modern=Path.Combine(Path.GetDirectoryName(corpus)!,"web-search.tsv");
        if(File.Exists(modern))
        {
            var lookup=rows.Select((r,i)=>(key:string.Join('\t',r.Split('\t',4).Take(3)),i)).ToDictionary(x=>x.key,x=>x.i);
            string[] modernRows=File.ReadAllLines(modern);string[] modernText=modernRows.Select(r=>r.Split('\t',4)[3]).ToArray();
            int[] modernIds=modernRows.Select(r=>lookup[string.Join('\t',r.Split('\t',4).Take(3))]).ToArray();
            var modernVectors=LoadVectors(Path.Combine(models,"web-vectors.bin"),Hash(modern)+identity,modernText,embed,build,e5?"passage: ":"");
            vectors=vectors.Concat(modernVectors).ToArray();texts=texts.Concat(modernText).ToArray();canonical.AddRange(modernIds);
        }
        if(build){Console.Error.WriteLine($"Index ready: {vectors.Length} passages across retrieval translations");return 0;}
        using var rerank=new Bert(Path.Combine(models,"reranker.onnx"),Path.Combine(models,"reranker-vocab.txt"));
        Console.WriteLine("READY");Console.Out.Flush();
        string? line;
        while((line=Console.ReadLine())!=null)
        {
            try
            {
                string query=Encoding.UTF8.GetString(Convert.FromBase64String(line));
                if(query.Length>4000)throw new ArgumentException("Query too long");
                var q=embed.Run(new[]{(e5?"query: ":"Represent this sentence for searching relevant passages: ")+query})[0];
                var best=Enumerable.Range(0,vectors.Length).Select(i=>{float score=0;for(int d=0;d<q.Length;d++)score+=q[d]*vectors[i][d];return (id:i,score);}).OrderByDescending(x=>x.score).Take(32).ToArray();
                var logits=rerank.Run(Enumerable.Repeat(query,best.Length).ToArray(),best.Select(x=>texts[x.id]).ToArray());
                var ranked=best.Select((x,i)=>(x.id,cosine:x.score,logit:logits[i][0])).OrderByDescending(x=>x.logit).GroupBy(x=>canonical[x.id]).Select(g=>g.First()).ToArray();
                // Conservative initial gate; experimental scores, not probabilities.
                Console.WriteLine(string.Join(";",ranked.Where(x=>args.Contains("--diagnostics") || (x.cosine>=0.55f && x.logit>=0)).Take(args.Contains("--diagnostics")?10:3).Select(x=>canonical[x.id]+","+x.cosine.ToString("R",CultureInfo.InvariantCulture)+","+x.logit.ToString("R",CultureInfo.InvariantCulture))));
                Console.Out.Flush();
            }
            catch(Exception ex){Console.Error.WriteLine(ex.Message);Console.WriteLine("ERROR");Console.Out.Flush();}
        }
        return 0;
    }
}
