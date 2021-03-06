﻿using System.Collections.Generic;
using umbraco.cms.businesslogic.relation;
using umbraco.DataLayer;

namespace OurUmbraco.Events.Relations
{
    public class EventRelation
    {
        private static string _event = "event";
        private static string _waitinglist = "waitinglist";

          
        public static void RelateMemberToEvent(int memberId, int eventId, string comment)
        {
            if (!Relation.IsRelated(eventId, memberId))
            {
                RelationType rt = RelationType.GetByAlias(_event);
                Relation.MakeNew(eventId, memberId, rt, comment);
            }
        }

        public static void PutMemberOnWaitingList(int memberId, int eventId, string comment)
        {
            if (!Relation.IsRelated(eventId, memberId))
            {
                RelationType rt = RelationType.GetByAlias(_waitinglist);
                Relation.MakeNew(eventId, memberId, rt, comment);
            }
        }

        private static int GetRelations(string alias, int parentId)
        {

            using (var sqlhelper = umbraco.BusinessLogic.Application.SqlHelper)
            {
                var result = sqlhelper.ExecuteScalar<int>(@"
                SELECT count(umbracoRelation.id)
                FROM umbracoRelation
                INNER JOIN umbracoRelationType ON umbracoRelationType.id = umbracoRelation.relType AND umbracoRelationType.alias = @alias
                where parentId = @parent", sqlhelper.CreateParameter("@parent", parentId), sqlhelper.CreateParameter("@alias", alias));

                return result;
            }
        }

        public static int GetSignedUp(int eventId)
        {
            return GetRelations(_event, eventId);
        }

        public static int GetWaiting(int eventId)
        {
            return GetRelations(_waitinglist, eventId);
        }

        private static List<Relation> GetRelations(string alias, string sort, int parentId, int number)
        {
            var retval = new List<Relation>();
            using (var sqlhelper = umbraco.BusinessLogic.Application.SqlHelper)
            {
                var query = string.Format(@"
                    SELECT TOP {0} umbracoRelation.id
                    FROM umbracoRelation
                    INNER JOIN umbracoRelationType ON umbracoRelationType.id = umbracoRelation.relType AND umbracoRelationType.alias = @alias
                    where parentId = @parent
                    ORDER BY datetime {1}                
                    ", number, sort);

                using (var reader = sqlhelper.ExecuteReader(query, sqlhelper.CreateParameter("@parent", parentId), sqlhelper.CreateParameter("@alias", alias)))
                {
                    while (reader.Read())
                        retval.Add(new Relation(reader.GetInt("id")));

                    return retval;
                }
            }
        }

        public static List<Relation> GetPeopleWaiting(int eventId, int number)
        {
            return GetRelations(_waitinglist, "ASC", eventId, number);
        }

        public static List<Relation> GetPeopleSignedUpLast(int eventId, int number)
        {
            return GetRelations(_event, "DESC", eventId, number);
        }


        public static void ClearRelations(int memberId, int eventId)
        {
            if (Relation.IsRelated(eventId, memberId))
            {
                foreach (Relation r in Relation.GetRelations(eventId))
                {
                    if (r.Child.Id == memberId)
                    {
                        r.Delete();
                        break;
                    }
                }
            }
        }
    }
}
