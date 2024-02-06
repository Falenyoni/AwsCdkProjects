using Amazon.CDK;
using Amazon.CDK.AWS.AppMesh;
using Amazon.CDK.AWS.EC2;
using Constructs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AwsCdkProjects
{
    public class AwsCdkProjectsStack : Stack
    {        
        internal AwsCdkProjectsStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var vpc = new Vpc(this, "MYVPC", new VpcProps
            {
                IpAddresses = IpAddresses.Cidr("10.0.0.0/16"),
                MaxAzs = 1,
                SubnetConfiguration = new ISubnetConfiguration[]
                {
                    new SubnetConfiguration
                    {
                        CidrMask = 24,
                        Name = "Public",
                        SubnetType= SubnetType.PUBLIC,
                        MapPublicIpOnLaunch = false
                    },
                    new SubnetConfiguration
                    {
                        CidrMask = 24,
                        Name = "Private",
                        SubnetType= SubnetType.PRIVATE_WITH_EGRESS,
                        MapPublicIpOnLaunch = false

                    }, new SubnetConfiguration
                    {
                        CidrMask = 28,
                        Name = "Isolated",
                        SubnetType = SubnetType.PRIVATE_ISOLATED,
                        MapPublicIpOnLaunch = false
                    }
                },
                NatGateways = 1
            });
            // associate an IPv6 ::/56 CIDR block with our vpc
            var cfnVpcCidrBlock = new CfnVPCCidrBlock(this, "Ipv6Cidr", new CfnVPCCidrBlockProps
            {
                VpcId = vpc.VpcId,
                AmazonProvidedIpv6CidrBlock = true
            });

            var vpcIpv6CidrBlock = Fn.Select(0, vpc.VpcIpv6CidrBlocks);

            // slice our ::/56 CIDR block into 256 chunks of ::/64 CIDRs
            var subnetIpv6CidrBlocks = Fn.Cidr(vpcIpv6CidrBlock, 256, "64");

            // associate an IPv6 CIDR sub-block to each subnet
            var allSubnets = vpc.PublicSubnets.Concat(vpc.PrivateSubnets).Concat(vpc.IsolatedSubnets).ToList();

            foreach (var (subnet, i) in allSubnets.Select((s, index) => (s, index)))
            {
                subnet.Node.AddDependency(cfnVpcCidrBlock);
                var cfnSubnet = subnet.Node.DefaultChild as CfnSubnet;
                cfnSubnet.Ipv6CidrBlock = Fn.Select(i, subnetIpv6CidrBlocks);
                cfnSubnet.AssignIpv6AddressOnCreation = true;
            }


            foreach (var subnet in allSubnets)
            {
                if (subnet is Subnet)
                {
                    ((Subnet)subnet).AddRoute("Default6Route", new AddRouteOptions
                    {
                        RouterId = vpc.InternetGatewayId,
                        RouterType = RouterType.GATEWAY,
                        DestinationIpv6CidrBlock = "::/0",
                        EnablesInternetConnectivity = true
                    });
                }
            }
            
        }


    }
}
