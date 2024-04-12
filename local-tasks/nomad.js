const gulp = requireModule('gulp');
const gUtil = require('gulp-util');
const { yellow} = require("ansi-colors");
const { ExecStepContext } = require("exec-step");
const system = requireModule("system");
const env = requireModule("env"),
    environmentVariables = {
        ENVIRONMENT: "ENVIRONMENT",
        IMAGE_TAG: "IMAGE_TAG",
        SECURITY_GROUP_ID: "SECURITY_GROUP_ID",
        CURRENT_IP: "CURRENT_IP",
        NOMAD_ADDR: "NOMAD_ADDR",
        NOMAD_TOKEN: "NOMAD_TOKEN",
        AMAZON_ACCOUNT_ID: "AMAZON_ACCOUNT_ID",
        AMAZON_REGION: "AMAZON_REGION"
    };


const projectName = "yumbi-iraas"

gulp.task("build-and-push-image", buildAndPushImageTask);
gulp.task("build-image", buildImageTask);
gulp.task("push-image", pushImageTask)

gulp.task("deploy", async () => {
    console.log('nomad deploy task for env:', env.resolveRequired(environmentVariables.ENVIRONMENT))
    const { runTask } = requireModule("run-task")
    await runTask("generate-nomad-job-spec");
    await runTask("setup-nomad-cli")
    await runTask("run-nomad-job");
});
gulp.task("add-ip-to-security-group", addIPToSecurityGroupTask);
gulp.task("remove-ip-from-security-group", removeIPFromSecurityGroupTask);


gulp.task("refresh-aws-docker-login", async () => {
    const
        ctx = new ExecStepContext(),
        accountId = env.resolveRequired(environmentVariables.AMAZON_ACCOUNT_ID),
        region = env.resolveRequired(environmentVariables.AMAZON_REGION);

    /**
     # * @type {SpawnResult}
     */
    const loginResult = await ctx.exec("fetch ecr login password token",
        () => system(
            "aws",
            ["ecr", "get-login-password", "--region", region], {
                suppressOutput: true
            })
    );
    const token = loginResult.stdout[0];
    await ctx.exec("login via docker",
        () => system("docker", [
            "login",
            "--username", "AWS",
            "--password", token,
            `${accountId}.dkr.ecr.${region}.amazonaws.com`
        ])
    );
});

async function buildAndPushImageTask() {
    const { runTask } = requireModule("run-task");

    const
        gUtil = requireModule("gulp-util"),
        { green } = require("ansi-colors");

    const tagExists = await testIfTagExists(projectName, await findImageTag());
    if (tagExists) {
        gUtil.log(green("Image tag already exists, skipping build and push."));
        return;
    }
    await Promise.all([
        await runTask("refresh-aws-docker-login"),
        await runTask("build-image")
    ])
    await runTask("push-image")
}
async function testIfTagExists(
    repository,
    imageTag
) {
    const { ECRClient, BatchGetImageCommand } = require("@aws-sdk/client-ecr"),
        client = new ECRClient({}),
        cmd = new BatchGetImageCommand({
            repositoryName: repository,
            imageIds: [
                { imageTag: imageTag }
            ]
        });

    try {
        const { images } = await client.send(cmd);
        const imageTagExists = (images || []).length > 0;
        if (imageTagExists) {
            gUtil.log(`${ repository }:${ imageTag } already exists`);
            return true;
        }
        gUtil.log(`image not found: ${ repository }:${ imageTag }`);
        return false;
    } catch (e) {
        if (!handledAccessDeniedError(e)) {
            throw e;
        }
    }
}

async function buildImageTask() {
    const
        imageTag = env.resolve(
            environmentVariables.IMAGE_TAG,
        ) || (await generateImageTag());


    const
        { ExecStepContext } = require("exec-step"),
        ctx = new ExecStepContext(),
        system = requireModule("system");
    await ctx.exec(`Publish IRAAS`,
        () => system(
            "dotnet",
            [
                "publish",
                `./src/IRAAS/IRAAS.csproj`,
                "-c",
                "ReleaseForDocker"
            ]));
    await ctx.exec(`Build docker image`,
        () => system(
            "docker",
            [
                "build",
                "-f",
                `Dockerfile`,
                "-t",
                `yumbi-iraas:${imageTag}`,
                "."
            ]));
}

async function generateImageTag() {
    const gitFactory = require("simple-git"),
        { fetchGitSha } = requireModule("git-sha");
    const
        git = gitFactory(),
        summary = await git.status(),
        shortSha = await fetchGitSha(".", true);
    return `${ sanitizeBranchToTagPart(summary.current) }-${ shortSha }`;
}

function sanitizeBranchToTagPart(branchName) {
    if (!branchName) {
        throw new Error(`no branch name supplied`);
    }
    let sanitized = branchName
        .replace(/\s+/g, "-")
        .replace(/\//g, "-");
    while (sanitized.indexOf("--") > -1) {
        sanitized = sanitized.replace(/--/g, "-");
    }
    return sanitized;
}
async function pushImageTask() {
    const
        amazonAccountId = env.resolveRequired(environmentVariables.AMAZON_ACCOUNT_ID),
        amazonRegion = env.resolveRequired(environmentVariables.AMAZON_REGION),
        imageTag = env.resolve(
            environmentVariables.IMAGE_TAG,
            environmentVariables.DOTNET_PUBLISH_CONTAINER_IMAGE_TAG
        ) || (await generateImageTag());
    const
        { ExecStepContext } = require("exec-step"),
        ctx = new ExecStepContext(),
        imageName = `${ amazonAccountId }.dkr.ecr.${ amazonRegion }.amazonaws.com/${ projectName }`,
        system = requireModule("system");

    const upstreamTag = await generateImageTag();

    await ctx.exec(`Tag image with ${ imageName }`,
        () => system("docker", [ "tag", `${ projectName }:${ imageTag }`, `${ imageName }:${ upstreamTag }` ])
    );
    await ctx.exec(`Push local docker image ${ imageName }:${ imageTag } upstream`,
        () => system("docker", [ "push", `${ imageName }:${ upstreamTag }` ])
    );
}
async function findImageTag() {
    const env = requireModule("env");
    return env.resolve(environmentVariables.IMAGE_TAG)
        || await generateImageTag();
}
async function addIPToSecurityGroupTask() {
    const {
            EC2Client,
            AuthorizeSecurityGroupIngressCommand
        } = require("@aws-sdk/client-ec2"),
        client = new EC2Client(),
        securityGroupId = env.resolveRequired(environmentVariables.SECURITY_GROUP_ID),
        currentIp = env.resolveRequired(environmentVariables.CURRENT_IP),
        cmd = new AuthorizeSecurityGroupIngressCommand(
            createIngressRequest(
                securityGroupId,
                currentIp));

    try {
        const result = await client.send(cmd);
        if (!result.Return) {
            // noinspection ExceptionCaughtLocallyJS
            throw new Error(`Unable to add ip to security group:\n${JSON.stringify(result, null, 2)}`)
        }
    } catch (e) {
        if (!handledAccessDeniedError(e)) {
            throw e;
        }
    }
}

async function removeIPFromSecurityGroupTask() {
    const {
            EC2Client,
            RevokeSecurityGroupIngressCommand
        } = require("@aws-sdk/client-ec2"),
        client = new EC2Client(),
        securityGroupId = env.resolveRequired(environmentVariables.SECURITY_GROUP_ID),
        currentIp = env.resolveRequired(environmentVariables.CURRENT_IP),
        cmd = new RevokeSecurityGroupIngressCommand(
            createIngressRequest(
                securityGroupId,
                currentIp));


    try {
        const result = await client.send(cmd);
        console.log(result);
        if (!result.Return) {
            // noinspection ExceptionCaughtLocallyJS
            throw new Error(`Unable to remove ip from security group:\n${JSON.stringify(result, null, 2)}`);
        }
    } catch (e) {
        if (isErrorSecurityGroupRuleNotFound(e)) {
            gUtil.log(yellow("Security group rule does not exist."));
            return;
        }
        if (!handledAccessDeniedError(e)) {
            throw e;
        }
    }
}

function createIngressRequest(securityGroupId, currentIp) {
    return {
        GroupId: securityGroupId,
        IpPermissions: [ {
            FromPort: 4646,
            ToPort: 4646,
            IpProtocol: "TCP",
            IpRanges: [ {
                CidrIp: `${ currentIp }/32`,
                Description: "Temporary ingress from a running GitHub action"
            } ]
        } ]
    };
}

function isErrorSecurityGroupRuleNotFound(error) {
    return error && error.Code === "InvalidPermission.NotFound";
}

function handledAccessDeniedError(e) {
    if (!isAccessDeniedError(e)) {
        return false;
    }

    throw new Error(`Access denied. Have you performed an 'aws configure' locally yet? If you have, perhaps you need more permissions:\n${ e.message }`);
}

function isAccessDeniedError(e) {
    return e && '__type' in e && e.__type === "AccessDeniedException";
}
