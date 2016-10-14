import React, {PropTypes} from "react";
import "./style.less";

const SocialPanelHeader = ({title, children}) => (
    <div className="socialpanelheader">
        <h3>{title}</h3>
        {children}
    </div>
);

SocialPanelHeader.propTypes = {
    title: PropTypes.string,
    children: PropTypes.node
};
export default SocialPanelHeader;