
using FastEndpoints;
using DbView.Core;
using DbView.Application;

namespace Users.Gets
{
    internal sealed class UserGetEndpoint : Endpoint<GetUserRequest, GetUserResponse, Mapper>
    {
        //MapsterMapper.IMapper mapper;
        
        IUserAppService userAppService;
        IConfiguration configuration;
        public UserGetEndpoint(IUserAppService userAppService, IConfiguration configuration)
        {
            this.userAppService = userAppService;
            this.configuration = configuration;
        }
        public override void Configure()
        {
            Get("/user/get");
            //AllowAnonymous();
            //Policies("admin");Roles("admin");
            //Description(b => b
            //   .Produces<GetUserResponse>(201)
            //    .ProducesProblem(400)
            //    .WithTags("Users"));

            Validator<GetUserValidator>();
            // 摘要信息
            // Swagger中摘要信息
            //Summary(s =>
            //{
            //    s.Summary = "接口名称";
            //    s.Description = "接口描述";
            //    s.ResponseExamples[201] = new GetUserResponse
            //    {
            //    };
            //});
        }

        public override async Task HandleAsync(GetUserRequest r, CancellationToken c)
        {
               // var userDto = await userAppService.GetUserAsync(UserId.From(r.Id), c);
                Response = new GetUserResponse
                {
                     Message = "OK",
                };
        }
    }
}

