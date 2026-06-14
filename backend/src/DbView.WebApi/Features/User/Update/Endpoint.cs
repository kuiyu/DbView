
using FastEndpoints;
using DbView.Core;
using DbView.Application;

namespace Users.Updates
{
    internal sealed class UserUpdateEndpoint : Endpoint<UpdateUserRequest, UpdateUserResponse, Mapper>
    {
        //MapsterMapper.IMapper mapper;
        
        IUserAppService userAppService;
        IConfiguration configuration;
        public UserUpdateEndpoint(IUserAppService userAppService, IConfiguration configuration)
        {
            this.userAppService = userAppService;
            this.configuration = configuration;
        }
        public override void Configure()
        {
            Get("/user/update");
            //AllowAnonymous();
            //Policies("admin");Roles("admin");
            //Description(b => b
            //   .Produces<UpdateUserResponse>(201)
            //    .ProducesProblem(400)
            //    .WithTags("Users"));

            Validator<UpdateUserValidator>();
            // 摘要信息
            // Swagger中摘要信息
            //Summary(s =>
            //{
            //    s.Summary = "接口名称";
            //    s.Description = "接口描述";
            //    s.ResponseExamples[201] = new UpdateUserResponse
            //    {
            //    };
            //});
        }

        public override async Task HandleAsync(UpdateUserRequest r, CancellationToken c)
        {
                Response = new UpdateUserResponse
                {
                     Message = "Not implemented yet",
                };
        }
    }
}

