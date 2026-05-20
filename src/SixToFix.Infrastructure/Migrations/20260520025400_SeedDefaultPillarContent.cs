using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SixToFix.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultPillarContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill default content for existing tenants with empty or placeholder pillar content
            // This migration updates any pillar_contents rows that have empty BodyJson
            
            var brandContent = """{"strategy":[{"title":"Define Your Identity","points":["Establish clear brand values and mission","Create consistent visual and verbal identity","Differentiate from competitors with unique positioning"]}],"execution":["Audit current brand perception across touchpoints","Document brand guidelines and style standards","Train team on consistent brand application"],"templates":[],"examples":[],"metrics":[]}""";
            var customerContent = """{"strategy":[{"title":"Know Your Audience","points":["Segment customers by behavior and value","Map the customer journey from awareness to advocacy","Prioritize high-value segments for targeted growth"]}],"execution":["Create detailed buyer personas with pain points","Identify key touchpoints in the customer lifecycle","Measure engagement and satisfaction at each stage"],"templates":[],"examples":[],"metrics":[]}""";
            var offeringContent = """{"strategy":[{"title":"Structure Your Portfolio","points":["Design clear service tiers with predictable pricing","Create productized offerings that scale","Align packages to customer maturity stages"]}],"execution":["Document all current services and their margins","Bundle complementary services into coherent packages","Establish renewal and upsell pathways"],"templates":[],"examples":[],"metrics":[]}""";
            var communicationContent = """{"strategy":[{"title":"Orchestrate Your Message","points":["Develop content that addresses each stage of the buyer journey","Coordinate messaging across channels for consistency","Measure engagement to refine content strategy"]}],"execution":["Map content types to buyer journey stages","Establish editorial calendar and approval workflow","Track content performance and optimize based on data"],"templates":[],"examples":[],"metrics":[]}""";
            var salesContent = """{"strategy":[{"title":"Systematize Revenue Generation","points":["Define repeatable sales stages and qualification criteria","Implement CRM to track pipeline and forecast accurately","Align sales and marketing on lead handoff and nurture"]}],"execution":["Document current sales process and identify gaps","Configure CRM with custom fields and automation","Train team on consistent qualification and follow-up"],"templates":[],"examples":[],"metrics":[]}""";
            var managementContent = """{"strategy":[{"title":"Drive Accountability","points":["Assign clear owners for each pillar and initiative","Establish KPIs that ladder up to business goals","Create regular review cadence to course-correct"]}],"execution":["Define roles and responsibilities across pillars","Set up dashboard to track progress and outcomes","Schedule monthly reviews with stakeholder accountability"],"templates":[],"examples":[],"metrics":[]}""";

            // Update Brand pillar (1)
            migrationBuilder.Sql($@"
                UPDATE pillar_contents
                SET body_json = '{brandContent}',
                    title = 'Brand Strategy',
                    subtitle = 'Build a powerful brand identity and positioning',
                    updated_at = NOW()
                WHERE pillar = 1
                  AND (body_json = '{{}}' 
                       OR body_json = '{{""placeholder"":true}}'
                       OR body_json = '{{""strategy"":[],""execution"":[],""templates"":[],""examples"":[],""metrics"":[]}}'
                      );
            ");

            // Update Customer pillar (2)
            migrationBuilder.Sql($@"
                UPDATE pillar_contents
                SET body_json = '{customerContent}',
                    title = 'Customer Strategy',
                    subtitle = 'Understand audience segments, acquisition priorities, and retention motion',
                    updated_at = NOW()
                WHERE pillar = 2
                  AND (body_json = '{{}}' 
                       OR body_json = '{{""placeholder"":true}}'
                       OR body_json = '{{""strategy"":[],""execution"":[],""templates"":[],""examples"":[],""metrics"":[]}}'
                      );
            ");

            // Update Offering pillar (3)
            migrationBuilder.Sql($@"
                UPDATE pillar_contents
                SET body_json = '{offeringContent}',
                    title = 'Offering Strategy',
                    subtitle = 'Package services for clarity, recurring revenue, and easier buying decisions',
                    updated_at = NOW()
                WHERE pillar = 3
                  AND (body_json = '{{}}' 
                       OR body_json = '{{""placeholder"":true}}'
                       OR body_json = '{{""strategy"":[],""execution"":[],""templates"":[],""examples"":[],""metrics"":[]}}'
                      );
            ");

            // Update Communication pillar (4)
            migrationBuilder.Sql($@"
                UPDATE pillar_contents
                SET body_json = '{communicationContent}',
                    title = 'Communication Strategy',
                    subtitle = 'Deliver the right message at the right time across all channels',
                    updated_at = NOW()
                WHERE pillar = 4
                  AND (body_json = '{{}}' 
                       OR body_json = '{{""placeholder"":true}}'
                       OR body_json = '{{""strategy"":[],""execution"":[],""templates"":[],""examples"":[],""metrics"":[]}}'
                      );
            ");

            // Update Sales pillar (5)
            migrationBuilder.Sql($@"
                UPDATE pillar_contents
                SET body_json = '{salesContent}',
                    title = 'Sales Strategy',
                    subtitle = 'Turn demand into predictable revenue with clear process and follow-through',
                    updated_at = NOW()
                WHERE pillar = 5
                  AND (body_json = '{{}}' 
                       OR body_json = '{{""placeholder"":true}}'
                       OR body_json = '{{""strategy"":[],""execution"":[],""templates"":[],""examples"":[],""metrics"":[]}}'
                      );
            ");

            // Update Management pillar (6)
            migrationBuilder.Sql($@"
                UPDATE pillar_contents
                SET body_json = '{managementContent}',
                    title = 'Management Strategy',
                    subtitle = 'Operationalize the framework with ownership, KPIs, and execution rhythm',
                    updated_at = NOW()
                WHERE pillar = 6
                  AND (body_json = '{{}}' 
                       OR body_json = '{{""placeholder"":true}}'
                       OR body_json = '{{""strategy"":[],""execution"":[],""templates"":[],""examples"":[],""metrics"":[]}}'
                      );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: reversing this would delete useful content
        }
    }
}
