
using FastEndpoints;
using DbView.Core;
using DbView.Application;

namespace Users.Deletes
{
    internal sealed class UserDeleteEndpoint : Endpoint<DeleteUserRequest, DeleteUserResponse, Mapper>
    {
        //MapsterMapper.IMapper mapper;
        
        IUserAppService userAppService;
        IConfiguration configuration;
        public UserDeleteEndpoint(IUserAppService userAppService, IConfiguration configuration)
        {
            this.userAppService = userAppService;
            this.configuration = configuration;
        }
        public override void Configure()
        {
            Delete("/user/delete");
            //AllowAnonymous();
            //Policies("admin");Roles("admin");
            //Description(b => b
            //   .Produces<DeleteUserResponse>(201)
            //    .ProducesProblem(400)
            //    .WithTags("Users"));

            Validator<DeleteUserValidator>();
            // 摘要信息
            // Swagger中摘要信息
            //Summary(s =>
            //{
            //    s.Summary = "接口名称";
            //    s.Description = "接口描述";
            //    s.ResponseExamples[201] = new DeleteUserResponse
            //    {
            //    };
            //});
        }

        public override async Task HandleAsync(DeleteUserRequest r, CancellationToken c)
        {
                Response = new DeleteUserResponse
                {
                     Message = "Not implemented yet",
                };
        }
    }
}

