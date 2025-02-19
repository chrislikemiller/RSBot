﻿namespace RSBot.Core.Client.ReferenceObjects
{
    public class RefQuest : IReference<uint>
    {
        #region Fields

        public byte Service;
        public uint ID;
        public string CodeName;
        public byte Level;
        public string DescName;
        public string NameString;
        public string PayString;
        public string ContentsString;
        public string PayContents;
        public string NoticeNPC;
        public string NoticeCondition;

        #endregion Fields

        public uint PrimaryKey => ID;

        public bool Load(ReferenceParser parser)
        {
            //Skip disabled
            if (!parser.TryParseByte(0, out Service) || Service == 0)
                return false;

            //Skip invalid ID (PK)
            if (!parser.TryParseUInt(1, out ID))
                return false;

            //Skip invalid CodeName
            if (!parser.TryParseString(2, out CodeName))
                return false;

            parser.TryParseByte(3, out Level);
            parser.TryParseString(4, out DescName);
            parser.TryParseString(5, out NameString);
            parser.TryParseString(6, out PayString);
            parser.TryParseString(7, out ContentsString);
            parser.TryParseString(8, out PayContents);
            parser.TryParseString(9, out NoticeNPC);
            parser.TryParseString(10, out NoticeCondition);

            return true;
        }
    }
}

//Service               1
//ID                    29
//CodeName              QSP_ALL_POTION_1
//[Level]               20
//DescName              재생의 물약 퀘스트
//NameString            SN_QSP_ALL_POTION_1
//PayString             SN_PAY_QSP_ALL_POTION_1
//ContentsString        xxx
//PayContents           SN_PAYCON_QSP_ALL_POTION_1
//NoticeNPC             SN_NN_QSP_ALL_POTION_1
//NoticeCondition       SN_NC_QSP_ALL_POTION_1