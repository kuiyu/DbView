using FastEndpoints;
using DbView.Core.Exceptions;
using System.Text.Json.Serialization;

public class GlobalResponseProcessor : IGlobalPostProcessor
{
    public async Task PostProcessAsync(IPostProcessorContext ctx, CancellationToken ct)
    {
        // 如果Endpoint已经手动发送了响应（设置了__manual_send标记），跳过
        if (ctx.HttpContext.Items.ContainsKey("__manual_send"))
        {
            return;
        }
        
        if (ctx.HttpContext.Response.HasStarted)
        {
            return;
        }
   
        if (ctx.ValidationFailures.Count > 0)
        {
            ctx.MarkExceptionAsHandled();//关键：不调用该方法会导致所有后处理器运行后自动抛出捕获的异常。
            var errMsg = (ctx.ValidationFailures.FirstOrDefault() as FluentValidation.Results.ValidationFailure).ErrorMessage;
            var res = ApiResponse.Fail(errMsg, ctx.ValidationFailures);
            await ctx.HttpContext.Response.WriteAsJsonAsync(res);
        }
        else
        {
            if (ctx.ExceptionDispatchInfo != null)
            {
                ctx.MarkExceptionAsHandled();
                if(ctx.ExceptionDispatchInfo.SourceException is DomainException)
                {
                    var res = ApiResponse.Fail(ctx.ExceptionDispatchInfo.SourceException.Message, null);
                    await ctx.HttpContext.Response.SendAsync(res);
                }
                else
                {
                    //记录日志
                    var res = ApiResponse.Error(ctx.ExceptionDispatchInfo.SourceException.Message, null);
                    await ctx.HttpContext.Response.SendAsync(res);
                }
                
            }
            else
            {
                ctx.MarkExceptionAsHandled();
                var res = ApiResponse.Ok("ok", ctx.Response);
                await ctx.HttpContext.Response.SendAsync(res);
            }
        }      
    }
}


public class ApiResponse
{
    public ApiResponse()
    {
        
    }
    /// <summary>
    /// 业务失败
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static ApiResponse Fail(string msg,object obj=null)
    {
        var response = new ApiResponse
        {
            Success = false,
            Code = 10500,
            Message = msg,
            Data = obj
        };
        return response;
    }
    /// <summary>
    /// 系统错误
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static ApiResponse Error(string msg, object obj = null)
    {
        var response = new ApiResponse
        {
            Success = false,
            Code = 500,
            Message = msg,
            Data = obj
        };
        return response;
    }
    public static ApiResponse Ok(string msg="ok",object obj=null)
    {
        var response = new ApiResponse
        {
            Success = true,
            Code =200,
            Message = msg,
            Data = obj
        };
        return response;
    }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
    [JsonPropertyName("code")]
    public int Code { get; set; } = 200;
    [JsonPropertyName("msg")]
    public string? Message { get; set; }
    [JsonPropertyName("result")]
    public object? Data { get; set; }
}



